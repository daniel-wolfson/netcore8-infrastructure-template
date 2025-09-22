using Confluent.Kafka;
using System.Threading.Channels;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// A high-performance Kafka consumer with optimized message processing pipeline
    /// </summary>
    public class KafkaConsumer : IKafkaConsumer, IDisposable, IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly ConsumerOptions _options;
        private readonly IConsumer<string, string> _consumer;
        private Func<ConsumeResult<string, string>, Task> _messageHandler;
        private readonly IConsumerDeliveryStrategy _consumerDelivery;
        private readonly TimeSpan _errorBackoff;
        private readonly TimeSpan _gracefulStopTimeout;
        private readonly IKafkaQueue? _deadLetterQueue;

        // Processing pipeline optimizations
        private readonly Channel<ConsumeResult<string, string>> _messageChannel;
        private readonly ChannelWriter<ConsumeResult<string, string>> _channelWriter;
        private readonly ChannelReader<ConsumeResult<string, string>> _channelReader;
        private readonly int _maxConcurrency;
        private readonly SemaphoreSlim _processingSemaphore;

        private CancellationTokenSource? _cts;
        private Task? _consumerTask;
        private Task[]? _processingTasks;
        private volatile int _started;
        private volatile int _stopping;
        private bool _disposed;

        public KafkaConsumer(
            ConsumerOptions options,
            ILogger logger,
            string topic,
            //Func<ConsumeResult<string, string>, Task> messageHandler,
            IConsumerDeliveryStrategy? deliveryStrategy = null,
            IKafkaQueue? deadLetterQueue = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            //_messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
            ArgumentException.ThrowIfNullOrWhiteSpace(topic);

            _errorBackoff = _options.RetryBackoffMs != default
                ? _options.RetryBackoffMs : TimeSpan.FromMilliseconds(1000);
            _gracefulStopTimeout = _options.HealthCheckTimeout ?? TimeSpan.FromSeconds(30);

            // ✅ Configure concurrent processing
            _maxConcurrency = Math.Max(1, Environment.ProcessorCount / 2);
            _processingSemaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);

            // ✅ Create bounded channel for message buffering
            var channelOptions = new BoundedChannelOptions(capacity: 1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            };

            _messageChannel = Channel.CreateBounded<ConsumeResult<string, string>>(channelOptions);
            _channelWriter = _messageChannel.Writer;
            _channelReader = _messageChannel.Reader;

            var consumerConfig = ConfigureConsumer(_options);
            _consumerDelivery = deliveryStrategy ?? DeliveryStrategyFactory.CreateConsumerStrategy(consumerConfig, _options);

            _consumer = new ConsumerBuilder<string, string>(consumerConfig)
                .SetErrorHandler((_, e) => _logger.Error("Kafka Consumer error: {Reason}", e.Reason))
                .SetLogHandler((_, m) => _logger.Debug("Kafka Consumer log: {Facility} {Message}", m.Facility, m.Message))
                .SetPartitionsAssignedHandler((c, partitions) =>
                {
                    var partitionInfo = string.Join(",", partitions.Select(p => $"{p.Topic}:{p.Partition}"));
                    _logger.Information("Partitions assigned: {Partitions}", partitionInfo);
                })
                .SetPartitionsRevokedHandler((c, partitions) =>
                {
                    var partitionInfo = string.Join(",", partitions.Select(p => $"{p.Topic}:{p.Partition}"));
                    _logger.Information("Partitions revoked: {Partitions}", partitionInfo);
                })
                .Build();

            _consumer.Subscribe(topic);

            // Initialize DLQ if needed
            if (options.EnableDeadLetterQueue && deadLetterQueue == null)
            {
                var dlqProducerConfig = new ProducerConfig
                {
                    BootstrapServers = options.BootstrapServers,
                    ClientId = $"{options.ClientId}-dlq",
                    EnableIdempotence = true,
                    LingerMs = 5,
                    BatchSize = 16384
                };
                _deadLetterQueue = new KafkaQueue(dlqProducerConfig, logger, options.DeadLetterQueueTopicSuffix);
            }
            else
            {
                _deadLetterQueue = deadLetterQueue;
            }
        }

        private ConsumerConfig ConfigureConsumer(ConsumerOptions options)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = options.BootstrapServers,
                GroupId = options.GroupId,
                ClientId = options.ClientId,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                
                // ✅ Optimized fetch settings for better throughput
                FetchMaxBytes = options.MaxFetchBytes,
                MaxPartitionFetchBytes = options.MaxPartitionFetchBytes,
                FetchMinBytes = 1024, // Wait for at least 1KB before returning
                FetchWaitMaxMs = 100,  // Max wait time for batch
                
                // ✅ Session and heartbeat optimizations
                SessionTimeoutMs = 30000,
                HeartbeatIntervalMs = 10000,
                MaxPollIntervalMs = 300000,
                
                // ✅ Reduce metadata refresh overhead
                MetadataMaxAgeMs = 300000,
                TopicMetadataRefreshIntervalMs = 300000
            };

            if (options.ConsumerConfig != null)
            {
                foreach (var kv in options.ConsumerConfig)
                    config.Set(kv.Key, kv.Value);
            }

            return config;
        }

        public Task SubscribeAsync(Func<ConsumeResult<string, string>, Task> messageHandler)
        {
            _messageHandler = messageHandler;

            if (Interlocked.Exchange(ref _started, 1) == 1)
            {
                _logger.Debug("Consumer already started");
                return Task.CompletedTask;
            }

            _cts = new CancellationTokenSource();

            // ✅ SubscribeAsync consumer task (producer to channel)
            _consumerTask = Task.Run(async () => await ConsumeToChannelAsync(_cts.Token), CancellationToken.None);

            // ✅ SubscribeAsync multiple processing tasks (consumers from channel)
            _processingTasks = new Task[_maxConcurrency];
            for (int i = 0; i < _maxConcurrency; i++)
            {
                var taskIndex = i;
                _processingTasks[i] = Task.Run(async () => await ProcessFromChannelAsync(taskIndex, _cts.Token), CancellationToken.None);
            }

            _logger.Information("Kafka consumer started with {Concurrency} processing threads", _maxConcurrency);
            return Task.CompletedTask;
        }

        public async Task UnsubscribeAsync()
        {
            if (Interlocked.Exchange(ref _stopping, 1) == 1)
            {
                _logger.Debug("Stop already in progress");
                if (_consumerTask != null)
                    await _consumerTask.ConfigureAwait(false);
                return;
            }

            try
            {
                if (_cts == null) return;

                _cts.Cancel();
                _channelWriter.Complete();

                // ✅ Wait for all tasks with timeout
                var allTasks = new List<Task>();
                if (_consumerTask != null) allTasks.Add(_consumerTask);
                if (_processingTasks != null) allTasks.AddRange(_processingTasks);

                var completedTask = await Task.WhenAny(
                    Task.WhenAll(allTasks),
                    Task.Delay(_gracefulStopTimeout)
                ).ConfigureAwait(false);

                if (completedTask != Task.WhenAll(allTasks))
                {
                    _logger.Warning("Consumer tasks did not complete within timeout, forcing close");
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
            }
        }

        // ✅ Optimized consume loop - single responsibility: feed the channel
        private async Task ConsumeToChannelAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(token);
                    if (result == null) continue;

                    // ✅ Non-blocking write to channel with backpressure handling
                    await _channelWriter.WriteAsync(result, token).ConfigureAwait(false);
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

            _channelWriter.Complete();
        }

        // ✅ Optimized processing loop - concurrent message handling
        private async Task ProcessFromChannelAsync(int workerIndex, CancellationToken token)
        {
            await foreach (var result in _channelReader.ReadAllAsync(token).ConfigureAwait(false))
            {
                await _processingSemaphore.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    await ProcessMessageWithRetryAsync(result, token, workerIndex).ConfigureAwait(false);
                }
                finally
                {
                    _processingSemaphore.Release();
                }
            }
        }

        private async Task ProcessMessageWithRetryAsync(ConsumeResult<string, string> result, 
            CancellationToken token, int workerIndex)
        {
            var attempts = 0;
            var maxRetries = _options.MaxRetries;

            while (attempts <= maxRetries && !token.IsCancellationRequested)
            {
                try
                {
                    // ✅ Execute handler without ConfigureAwait in user code path
                    await _messageHandler(result);
                    await _consumerDelivery.HandleAfterProcessAsync(_consumer, result).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    attempts++;

                    if (!IsRetryableException(ex) || attempts > maxRetries)
                    {
                        await HandlePermanentFailure(result, ex).ConfigureAwait(false);
                        return;
                    }

                    var backoff = CalculateBackoff(attempts);
                    _logger.Warning(ex, "Worker {WorkerIndex}: Retry {Attempt}/{MaxRetries} after {Delay}ms for partition {Partition} offset {Offset}",
                        workerIndex, attempts, maxRetries, backoff.TotalMilliseconds, result.Partition, result.Offset);

                    await DelayWithCancellation(backoff, token).ConfigureAwait(false);
                }
            }
        }

        private async Task HandlePermanentFailure(ConsumeResult<string, string> result, Exception ex)
        {
            _logger.Error(ex, "Message permanently failed after {MaxRetries} attempts for topic {Topic} partition {Partition} offset {Offset}",
                _options.MaxRetries, result.Topic, result.Partition, result.Offset);

            try
            {
                if (_deadLetterQueue != null)
                {
                    var message = new KafkaMessage
                    {
                        Topic = result.Topic,
                        Partition = result.Partition.Value,
                        Offset = result.Offset.Value,
                        Value = result.Message.Value,
                        Key = result.Message.Key
                    };

                    await _deadLetterQueue.PublishAsync(message, ex, _options.MaxRetries + 1).ConfigureAwait(false);
                }

                // ✅ Always commit to prevent infinite retry loops
                await _consumerDelivery.HandleAfterProcessAsync(_consumer, result).ConfigureAwait(false);
            }
            catch (Exception commitEx)
            {
                _logger.Error(commitEx, "Failed to handle permanent failure cleanup");
            }
        }

        private static bool IsRetryableException(Exception ex)
        {
            // ✅ Use pattern matching for better performance
            return ex is not (ArgumentException or ArgumentNullException or InvalidOperationException or FormatException);
        }

        private TimeSpan CalculateBackoff(int attempt)
        {
            // ✅ Optimized exponential backoff with jitter
            var baseMs = (int)_options.RetryBackoffMs.TotalMilliseconds;
            var exponentialMs = Math.Min(baseMs * (1 << (attempt - 1)), 30000); // Cap at 30s
            var jitterMs = Random.Shared.Next(0, Math.Max(1, exponentialMs / 10));
            return TimeSpan.FromMilliseconds(exponentialMs + jitterMs);
        }

        // ✅ Helper method to avoid exception allocation during normal cancellation
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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
                    _processingSemaphore?.Dispose();
                    _cts?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.Warning(ex, "Error during disposal");
                }
            }
        }
    }
}
