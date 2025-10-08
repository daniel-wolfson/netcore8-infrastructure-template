using Confluent.Kafka;
using Custom.Framework.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// A high-performance Kafka consumer with optimized message processing pipeline
    /// </summary>
    public class KafkaConsumer : IKafkaConsumer, IDisposable, IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly KafkaOptions _options;
        private readonly TimeSpan _errorBackoff;
        private readonly int _maxConcurrency;

        private readonly IConsumer<string, byte[]> _consumer;
        private readonly IConsumerDeliveryStrategy _deliveryStrategy;
        private readonly Channel<ConsumeResult<string, byte[]>> _messageChannel;
        private readonly ChannelWriter<ConsumeResult<string, byte[]>> _channelWriter;
        private readonly ChannelReader<ConsumeResult<string, byte[]>> _channelReader;
        private CancellationTokenSource? _cts;
        private Task? _consumerTask;
        private Task[]? _processingTasks;
        private volatile int _started;
        private CancellationToken _cancelToken;
        private volatile int _stopping;

        private bool _disposed;

        public string[] Topics => _options.Consumers.SelectMany(x => x.Topics).ToArray();

        public DateTime LastAccessTime { get; set; }
        public int AccessCount { get; set; }
        public DateTime CreatedTime { get; set; }
        public string GroupId { get; }
        public string Name { get; }

        public KafkaConsumer(string name, KafkaOptions options,
            IConsumerDeliveryStrategy deliveryStrategy, ILogger logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            //_kafkaOptions.GroupId = _kafkaOptions.ReplaceTemplate(_kafkaOptions.GroupId);
            _cts = new CancellationTokenSource();
            _cancelToken = _cts.Token;
            _maxConcurrency = Math.Max(1, Environment.ProcessorCount / 2);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var settings = _options.Consumers.FirstOrDefault(x => x.Name == name)
                ?? throw new NullReferenceException("No consumer settings defined");
            _errorBackoff = settings.RetryBackoffMs != default ? settings.RetryBackoffMs : Timeouts.RetryBackoffMs;

            GroupId = settings.GroupId;
            Name = settings.Name;

            // Create bounded channel for message buffering
            var channelOptions = new BoundedChannelOptions(capacity: settings.ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            };
            _messageChannel = Channel.CreateBounded<ConsumeResult<string, byte[]>>(channelOptions);
            _channelWriter = _messageChannel.Writer;
            _channelReader = _messageChannel.Reader;

            _deliveryStrategy = _deliveryStrategy
                ?? DeliveryStrategyFactory.CreateConsumerStrategy(settings.DeliverySemantics, settings);

            _consumer = new ConsumerBuilder<string, byte[]>(_deliveryStrategy.ConsumerConfig)
                .SetErrorHandler((_, e) =>
                    _logger.Error("ConsumerBuilder.SetErrorHandler: Kafka Consumer error: {Reason}", e.Reason))
                .SetLogHandler((_, m) =>
                    _logger.Debug("ConsumerBuilder.SetLogHandler: Kafka Consumer log: {Facility} {Message}", m.Facility, m.Message))
                .SetPartitionsAssignedHandler((c, partitions) =>
                    _logger.Information("ConsumerBuilder.SetPartitionsAssignedHandler: Partitions assigned to: {Partitions}", partitions.ToListString())
                )
                .SetPartitionsRevokedHandler((c, partitions) =>
                {
                    var partitionInfo = string.Join(",", partitions.Select(p => $"{p.Topic}:{p.Partition}"));
                    _logger.Information("ConsumerBuilder.SetPartitionsRevokedHandler: Partitions revoked: {Partitions}", partitionInfo);
                })
                .Build();
        }

        #region public methods

        /// <summary>
        /// Begins consuming messages from the configured Kafka topics and dispatches them to the specified message
        /// handler for processing. Performs once only, subsequent calls have no effect if the consumer is already
        /// </summary>
        public void Subscribe(string[]? topics,
            Func<ConsumeResult<string, byte[]>, CancellationToken, Task>
            messageHandler)
        {
            // Reset stopping flag if we're restarting
            _stopping = 0;

            if (Interlocked.Exchange(ref _started, 1) == 1)
            {
                _logger.Warning("Consumer already started");
                return;
            }

            // SubscribeAsync consumer task (producer to channel)
            _consumerTask = Task.Run(async () =>
            {
                await ConsumeToChannelAsync(_cancelToken);
            });

            // SubscribeAsync multiple processing tasks (consumers from channel)
            _processingTasks = new Task[_maxConcurrency];
            for (int i = 0; i < _maxConcurrency; i++)
            {
                var taskIndex = i;
                _processingTasks[i] = Task.Run(async () =>
                {
                    await ProcessFromChannelAsync(taskIndex, messageHandler, _cancelToken);
                });
            }

            topics = (topics != null && topics.Length > 0)
                ? topics
                : _options.Consumers.SelectMany(x => x.Topics).ToArray();

            if (topics.Length > 0)
                _consumer.Subscribe(topics);

            _logger.Information("Kafka consumer started with {Concurrency} processing threads", _maxConcurrency);
        }

        public void Subscribe<TMessage>(string[]? topics,
            Func<TMessage?, object, CancellationToken, Task> messageHandler)
        {
            Subscribe(topics, async (result, token) =>
            {
                try
                {
                    var jsonString = Encoding.UTF8.GetString(result.Message.Value);
                    var message = JsonSerializer.Deserialize<TMessage>(jsonString);

                    await messageHandler(message, result, _cancelToken);
                }
                catch (JsonException jsonEx)
                {
                    var jsonString = Encoding.UTF8.GetString(result.Message.Value);
                    _logger.Error(jsonEx, "Failed to deserialize message from topic {Topic}, partition {Partition}, offset {Offset}. Raw JSON: {RawJson}",
                        result.Topic, result.Partition.Value, result.Offset.Value, jsonString);
                    HandlePermanentFailure(result, jsonEx);
                    await messageHandler(default, result, _cancelToken);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error processing message from topic {Topic}", result.Topic);
                    HandlePermanentFailure(result, ex);
                    await messageHandler(default, result, _cancelToken);
                }
            });
        }

        public void Subscribe(Func<ConsumeResult<string, byte[]>, CancellationToken, Task> messageHandler)
        {
            Subscribe(null, messageHandler);
        }
        public void Subscribe<TMessage>(Func<TMessage?, object, CancellationToken, Task> messageHandler)
        {
            Subscribe<TMessage>(null, messageHandler);
        }

        public async Task UnsubscribeAsync()
        {
            try
            {
                if (Interlocked.Exchange(ref _stopping, 1) == 1)
                {
                    _logger.Debug("Stop already in progress");
                    if (_consumerTask != null && _consumerTask.Status != TaskStatus.Faulted)
                        await _consumerTask.ConfigureAwait(false);
                    return;
                }

                if (_cts == null) return;

                _channelWriter.TryComplete();
                _cts.Cancel();

                // Wait for all tasks with timeout
                var allTasks = new List<Task>();
                if (_consumerTask != null) allTasks.Add(_consumerTask);
                if (_processingTasks != null) allTasks.AddRange(_processingTasks);

                var completedTask = await Task.WhenAny(
                    Task.WhenAll(allTasks),
                    Task.Delay(Timeouts.ConsumerUnsubscribeStop * allTasks.Count)
                ).ConfigureAwait(false);

                if (completedTask != Task.WhenAll(allTasks))
                {
                    _logger.Warning("{Title}: Consumer tasks did not complete within timeout, forcing close", nameof(UnsubscribeAsync));
                    _consumer.Close();
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error while stopping consumer");
            }
            finally
            {
                try { _consumer.Close(); } catch { }
                try { _cts?.Dispose(); } catch { }
                _cts = null;
                _started = 0;
                _stopping = 0;
                try { await Task.Delay(Timeouts.ConsumerUnsubscribeStop); } catch { }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                await UnsubscribeAsync().ConfigureAwait(false);
            }
            finally
            {
                Dispose(false);
                GC.SuppressFinalize(this);
            }
        }

        public async Task FlushAsync(TimeSpan? span = null)
        {
            var messageCount = GetTotalMessagesInTopics();
            if (messageCount == 0)
            {
                return;
            }
            
            // Create completion task that polls until all messages are processed
            var completionTask = Task.Run(async () =>
            {
                while (!_cancelToken.IsCancellationRequested)
                {
                    var loopStartTime = DateTimeOffset.UtcNow;
                    
                    var remainingMessages = GetTotalMessagesInTopics();
                    if (remainingMessages == 0)
                    {
                        break;
                    }

                    // Poll interval - adjust based on message processing rate
                    await Task.Delay(span ?? Timeouts.MessageDelivery, _cancelToken).ConfigureAwait(false);
                }
            }, _cancelToken);

            var timeout = Task.Delay(span ?? Timeouts.MessageDelivery * messageCount, _cancelToken);
            await Task.WhenAny(completionTask, timeout).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the number of unprocessed messages currently in the internal processing channel.
        /// These are messages that have been consumed from Kafka but not yet processed by workers.
        /// </summary>
        /// <returns>The count of messages waiting in the channel to be processed</returns>
        public int GetPendingMessageCount()
        {
            try
            {
                // Get the count of messages in the channel waiting to be processed
                // This uses the Reader's Count property which returns items available to read
                return _channelReader.Count;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "{Title}: Failed to get pending message count", nameof(GetPendingMessageCount));
                return 0;
            }
        }

        /// <summary>
        /// Gets the total number of messages that exist in Kafka topics (high watermark).
        /// This counts ALL messages in the topic, regardless of whether they've been consumed or not.
        /// </summary>
        /// <returns>Total message count across all partitions in subscribed topics</returns>
        public long GetTotalMessagesInTopics()
        {
            long totalMessages = 0;

            try
            {
                // Get assigned partitions
                var assignment = _consumer.Assignment;

                if (assignment == null || assignment.Count == 0)
                {
                    _logger.Debug("{Title}: No partitions assigned to consumer", nameof(GetTotalMessagesInTopics));
                    return totalMessages;
                }

                foreach (var partition in assignment)
                {
                    try
                    {
                        // Get watermark offsets
                        var watermarkOffsets = _consumer.QueryWatermarkOffsets(partition, TimeSpan.FromSeconds(5));

                        // High watermark = total messages in this partition
                        // (it's the offset of the next message to be written, so it equals the count)
                        var messageCount = watermarkOffsets.High.Value;

                        totalMessages += messageCount;

                        _logger.Debug("{Title}: Partition {Partition} has {Count} total messages (high watermark: {High})",
                            nameof(GetTotalMessagesInTopics),
                            $"{partition.Topic}:{partition.Partition}",
                            messageCount,
                            watermarkOffsets.High.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "{Title}: Failed to get message count for partition {Partition}",
                            nameof(GetTotalMessagesInTopics), $"{partition.Topic}:{partition.Partition}");
                    }
                }

                _logger.Information("{Title}: Total messages in topic: {TotalCount}",
                    nameof(GetTotalMessagesInTopics), totalMessages);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "{Title}: Failed to get total message count", nameof(GetTotalMessagesInTopics));
            }

            return totalMessages;
        }

        /// <summary>
        /// Gets detailed message count information per partition.
        /// Shows total messages, consumed position, and remaining (lag) for each partition.
        /// </summary>
        /// <returns>Dictionary with partition info including total messages, position, and lag</returns>
        public Dictionary<string, PartitionMessageInfo> GetPartitionMessageInfo()
        {
            var partitionInfo = new Dictionary<string, PartitionMessageInfo>();

            try
            {
                var assignment = _consumer.Assignment;

                if (assignment == null || assignment.Count == 0)
                {
                    _logger.Warning("{Title}: No partitions assigned to consumer", nameof(GetPartitionMessageInfo));
                    return partitionInfo;
                }

                foreach (var partition in assignment)
                {
                    try
                    {
                        var position = _consumer.Position(partition);
                        var watermarkOffsets = _consumer.QueryWatermarkOffsets(partition, TimeSpan.FromSeconds(5));

                        var key = $"{partition.Topic}:{partition.Partition}";
                        partitionInfo[key] = new PartitionMessageInfo
                        {
                            Topic = partition.Topic,
                            Partition = partition.Partition.Value,
                            TotalMessages = watermarkOffsets.High.Value,
                            ConsumedPosition = position.Value,
                            RemainingMessages = watermarkOffsets.High.Value - position.Value,
                            LowWatermark = watermarkOffsets.Low.Value,
                            HighWatermark = watermarkOffsets.High.Value
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "{Title}: Failed to get info for partition {Partition}",
                            nameof(GetPartitionMessageInfo), $"{partition.Topic}:{partition.Partition}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "{Title}: Failed to get partition message info", nameof(GetPartitionMessageInfo));
            }

            return partitionInfo;
        }

        #endregion public methods

        #region private methods

        /// <summary>
        /// Optimized consume loop - single responsibility: feed the channel.
        /// Performed: AFTER CONSUMING
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task ConsumeToChannelAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {

                try
                {
                    var consumeResult = _consumer.Consume(token);
                    if (consumeResult == null)
                    {
                        _logger.Error("{Title} consumeResult is null", ApiHelper.LogTitle());
                        continue;
                    }

                    if (consumeResult.IsPartitionEOF)
                    {
                        _logger.Information("Reached end of topic {Topic}, partition {Partition}, offset {Offset}.",
                            consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
                        continue;
                    }

                    // Non-blocking write to channel with backpressure handling
                    await _channelWriter.WriteAsync(consumeResult, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    _logger.Error(ex, "Consume exception: {Reason}", ex.Error.Reason);
                    await DelayWithCancellation(_errorBackoff, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unexpected consume error");
                    await DelayWithCancellation(_errorBackoff, token).ConfigureAwait(false);
                }
            }

            _channelWriter.TryComplete();
        }

        /// <summary>
        /// Processing loop - concurrent message handling.
        /// </summary>
        private async Task ProcessFromChannelAsync(int workerIndex,
            Func<ConsumeResult<string, byte[]>, CancellationToken, Task> messageHandler,
            CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;

            try
            {
                await foreach (var result in _channelReader.ReadAllAsync(token).ConfigureAwait(false))
                {
                    try
                    {
                        await ProcessMessageAsync(result, messageHandler, token, workerIndex).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "{Title} Worker {WorkerIndex} failed to process message",
                            ApiHelper.LogTitle(), workerIndex);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Expected during shutdown - exit gracefully
                return;
            }
        }

        /// <summary>
        /// Processes a consumed message using the specified handler, applying a single retry with backoff 
        /// if a retryable exception occurs. 
        /// Performed: AFTER PROCESSING, BEFORE COMMIT
        /// </summary>
        private async Task ProcessMessageAsync(
            ConsumeResult<string, byte[]> result,
            Func<ConsumeResult<string, byte[]>, CancellationToken, Task> messageHandler,
            CancellationToken token, int workerIndex)
        {
            if (token.IsCancellationRequested)
                return;

            try
            {
                await messageHandler(result, token);
                _deliveryStrategy.HandleAfterProcess(_consumer, result);
            }
            catch (Exception ex)
            {
                if (IsRetryableException(ex))
                {
                    var backoff = CalculateBackoff(1);
                    _logger.Warning(ex, "Worker {WorkerIndex}: Retrying after {Delay}ms for partition {Partition} offset {Offset}",
                        workerIndex, backoff.TotalMilliseconds, result.Partition, result.Offset);
                    await DelayWithCancellation(backoff, token);

                    // retry once
                    try
                    {
                        await messageHandler(result, token);
                        _deliveryStrategy.HandleAfterProcess(_consumer, result);
                    }
                    catch (Exception retryEx)
                    {
                        HandlePermanentFailure(result, retryEx);
                    }
                }
                else
                {
                    HandlePermanentFailure(result, ex);
                }
            }
        }

        private void HandlePermanentFailure(ConsumeResult<string, byte[]> result, Exception ex)
        {

            _logger.Error(ex, "Message permanently failed for topics {Name} partition {Partition} offset {Offset}",
                result.Topic, result.Partition, result.Offset);

            var headers = result.Message.Headers ?? [];
            try
            {
                headers.Add(new Header("TopicSource", Encoding.UTF8.GetBytes(result.Topic)));
                headers.Add(new Header("PartitionSource", Encoding.UTF8.GetBytes(result.Partition.Value.ToString())));
                headers.Add(new Header("OffsetSource", Encoding.UTF8.GetBytes(result.Offset.Value.ToString())));
                headers.Add(new Header("Exception", Encoding.UTF8.GetBytes(ex.ToString())));

                // ✅ Always commit to prevent infinite retry loops
                _deliveryStrategy.HandleAfterProcess(_consumer, result);
            }
            catch (Exception handleEx)
            {
                headers.Add(new Header("Exception", Encoding.UTF8.GetBytes(handleEx.ToString())));
                _logger.Error(handleEx, "Failed to handle permanent failure cleanup");
            }
        }

        private static bool IsRetryableException(Exception ex)
        {
            return ex is not (ArgumentException
                or ArgumentNullException
                or InvalidOperationException
                or FormatException);
        }

        // Exponential backoff with jitter to avoid thundering herd
        private TimeSpan CalculateBackoff(int attempt)
        {
            // Optimized exponential backoff with jitter 
            var setings = _options.Consumers.FirstOrDefault(x => x.Name == Name);
            var baseMs = setings?.RetryBackoffMs.Milliseconds ?? Timeouts.RetryBackoffMs.Milliseconds;
            var exponentialMs = Math.Min(baseMs * (1 << (attempt - 1)), 30000); // Cap at 30s
            var jitterMs = Random.Shared.Next(0, Math.Max(1, exponentialMs / 10));
            return TimeSpan.FromMilliseconds(exponentialMs + jitterMs);
        }

        // Helper method to avoid exception allocation during normal cancellation
        private static async Task DelayWithCancellation(TimeSpan delay, CancellationToken token)
        {
            try
            {
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Expected during shutdown
            }
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                try
                {
                    if (Interlocked.Exchange(ref _stopping, 1) == 0)
                        _cts?.Cancel();

                    _consumerTask?.Wait(5000);
                    if (_processingTasks != null)
                        Task.WaitAll(_processingTasks, 5000);

                    _consumer?.Close();
                    _consumer?.Dispose();
                    _cts?.Dispose();
                }
                catch (ObjectDisposedException ex) when (ex.Message == "handle is destroyed")
                {
                    // Expected when _consumer allready closed
                }
                catch (Exception ex)
                {
                    _logger?.Warning(ex, "Error during disposal");
                }
            }
        }

        #endregion private methods
    }
}
