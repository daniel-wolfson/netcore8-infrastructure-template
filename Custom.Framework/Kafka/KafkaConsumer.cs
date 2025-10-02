using Confluent.Kafka;
using Custom.Domain.Optima.Models.Main;
using Custom.Framework.Helpers;
using Custom.Framework.Models.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// A high-performance Kafka consumer with optimized message processing pipeline
    /// </summary>
    public class KafkaConsumer : IKafkaConsumer, IDisposable, IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly ConsumerSettings _settings;
        private readonly TimeSpan _errorBackoff;
        private readonly IConsumer<string, byte[]> _consumer;
        private readonly IConsumerDeliveryStrategy _consumerDelivery;

        // Processing pipeline optimizations
        private readonly Channel<ConsumeResult<string, byte[]>> _messageChannel;
        private readonly ChannelWriter<ConsumeResult<string, byte[]>> _channelWriter;
        private readonly ChannelReader<ConsumeResult<string, byte[]>> _channelReader;
        private readonly int _maxConcurrency;

        private CancellationTokenSource? _cts;
        private Task? _consumerTask;
        private Task[]? _processingTasks;
        private volatile int _started;
        private CancellationToken? _cancelToken;
        private volatile int _stopping;
        private bool _disposed;

        public string[] Topics => _settings.Topics;

        public KafkaConsumer(ConsumerSettings settings, ILogger logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            //_settings.GroupId = ApiTemplateHelper.ReplaceTemplate(_settings.GroupId);
            //_settings.ClientId = ApiTemplateHelper.ReplaceTemplate(_settings.ClientId);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cts = new CancellationTokenSource();
            _cancelToken = _cts.Token;
            _errorBackoff = _settings.RetryBackoffMs != default ? _settings.RetryBackoffMs : TimeSpan.FromMilliseconds(1000);
            _maxConcurrency = Math.Max(1, Environment.ProcessorCount / 2);
           
            // Create bounded channel for message buffering
            var channelOptions = new BoundedChannelOptions(capacity: 1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            };

            _messageChannel = Channel.CreateBounded<ConsumeResult<string, byte[]>>(channelOptions);
            _channelWriter = _messageChannel.Writer;
            _channelReader = _messageChannel.Reader;

            var consumerConfig = ConfigureConsumer(_settings);
            _consumerDelivery = DeliveryStrategyFactory.CreateConsumerStrategy(consumerConfig, _settings);

            _consumer = new ConsumerBuilder<string, byte[]>(consumerConfig)
                //.SetValueDeserializer(new KafkaMessageDeserializer<TMessage>())
                .SetErrorHandler((_, e) =>
                    _logger.Error("{Title} Kafka Consumer error: {Reason}", ApiHelper.LogTitle(), e.Reason))
                .SetLogHandler((_, m) =>
                    _logger.Debug("{Title} Kafka Consumer log: {Facility} {Message}", ApiHelper.LogTitle(), m.Facility, m.Message))
                .SetPartitionsAssignedHandler((c, partitions) =>
                    _logger.Information("SetPartitionsAssignedHandler: Partitions assigned to: {Partitions}", partitions.ToListString())
                )
                .SetPartitionsRevokedHandler((c, partitions) =>
                {
                    var partitionInfo = string.Join(",", partitions.Select(p => $"{p.Topic}:{p.Partition}"));
                    _logger.Information("SetPartitionsRevokedHandler: Partitions revoked: {Partitions}", partitionInfo);
                })
                .Build();
        }

        public KafkaConsumer(string consumerName, IOptionsMonitor<ConsumerSettings> options, ILogger logger)
            : this(options.Get(consumerName), logger)
        {
        }

        #region public methods

        /// <summary>
        /// Begins consuming messages from the configured Kafka topics and dispatches them to the specified message
        /// handler for processing. Performs once only, subsequent calls have no effect if the consumer is already
        /// </summary>
        public void Subscribe(
            Func<ConsumeResult<string, byte[]>, CancellationToken?, Task>
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
                await ConsumeToChannelAsync(_cancelToken ?? CancellationToken.None));

            // SubscribeAsync multiple processing tasks (consumers from channel)
            _processingTasks = new Task[_maxConcurrency];
            for (int i = 0; i < _maxConcurrency; i++)
            {
                var taskIndex = i;
                _processingTasks[i] = Task.Run(async () =>
                    await ProcessFromChannelAsync(taskIndex, messageHandler, _cancelToken ?? CancellationToken.None));
            }

            _consumer.Subscribe(_settings.Topics);

            _logger.Information("Kafka consumer started with {Concurrency} processing threads", _maxConcurrency);
        }

        public void Subscribe<TMessage>(
            Func<TMessage?, object, CancellationToken?, Task> messageHandler)
        {
            Subscribe(async (result, token) =>
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

        #endregion public methods

        #region private methods

        private static ConsumerConfig ConfigureConsumer(ConsumerSettings options)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = options.BootstrapServers,
                GroupId = options.GroupId,
                ClientId = options.ClientId,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,

                // ✅ Optimized fetch settings for better throughput
                FetchMaxBytes = options.FetchMaxBytes,
                MaxPartitionFetchBytes = options.MaxPartitionFetchBytes,
                FetchMinBytes = options.FetchMinBytes, // Wait for at least 1KB before returning
                FetchWaitMaxMs = 100,  // Max wait time for batch

                // ✅ Session and heartbeat optimizations
                SessionTimeoutMs = 30000,
                HeartbeatIntervalMs = 10000,
                MaxPollIntervalMs = 300000,

                // ✅ Reduce metadata refresh overhead
                MetadataMaxAgeMs = 300000,
                TopicMetadataRefreshIntervalMs = 300000
            };

            if (options.CustomConsumerConfig != null)
            {
                foreach (var kv in options.CustomConsumerConfig)
                    config.Set(kv.Key, kv.Value);
            }

            return config;
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

            _channelWriter.TryComplete();
        }

        /// <summary>
        /// Processing loop - concurrent message handling.
        /// </summary>
        private async Task ProcessFromChannelAsync(int workerIndex,
            Func<ConsumeResult<string, byte[]>, CancellationToken?, Task> messageHandler,
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
                        await ProcessMessageWithRetryAsync(result, messageHandler, token, workerIndex).ConfigureAwait(false);
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

        private async Task ProcessMessageWithRetryAsync(
            ConsumeResult<string, byte[]> result,
            Func<ConsumeResult<string, byte[]>, CancellationToken?, Task> messageHandler,
            CancellationToken token, int workerIndex)
        {
            if (token.IsCancellationRequested)
                return;

            try
            {
                await messageHandler(result, token);
                _consumerDelivery.HandleAfterProcess(_consumer, result);
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
                        _consumerDelivery.HandleAfterProcess(_consumer, result);
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
            _logger.Error(ex, "Message permanently failed after {MessageSendMaxRetries} attempts for topics {Name} partition {Partition} offset {Offset}",
                _settings.MaxRetries, result.Topic, result.Partition, result.Offset);

            var headers = result.Message.Headers ?? [];
            try
            {
                headers.Add(new Header("TopicSource", Encoding.UTF8.GetBytes(result.Topic)));
                headers.Add(new Header("PartitionSource", Encoding.UTF8.GetBytes(result.Partition.Value.ToString())));
                headers.Add(new Header("OffsetSource", Encoding.UTF8.GetBytes(result.Offset.Value.ToString())));
                headers.Add(new Header("Exception", Encoding.UTF8.GetBytes(ex.ToString())));

                // ✅ Always commit to prevent infinite retry loops
                _consumerDelivery.HandleAfterProcess(_consumer, result);
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
            var baseMs = (int)_settings.RetryBackoffMs.TotalMilliseconds;
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

        private static string FormatTemplate(object settings,
            string fieldName)
        {
            var dicSettings = settings.GetType()
                .GetProperties()
                .Where(p => p.CanRead && p.CanWrite)
                .ToDictionary(p => p.Name, p => p.GetValue(settings)?.ToString() ?? string.Empty);

            if (!dicSettings.TryGetValue(fieldName, out var field) || string.IsNullOrEmpty(field))
                return string.Empty;

            var template = dicSettings[fieldName];
            if (field.Contains(template, StringComparison.Ordinal))
            {
                return field.Replace(template, dicSettings[fieldName], StringComparison.Ordinal);
            }

            return field;
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
