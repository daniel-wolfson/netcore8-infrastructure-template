using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// A Kafka consumer that subscribes to a topic, continuously consumes messages,
    /// invokes a provided message handler, and delegates commit/retry behavior to a delivery strategy.
    /// Optimized for safer start/stop, deterministic disposal and better logging around shutdown.
    /// </summary>
    public class KafkaConsumer : IKafkaConsumer, IDisposable, IAsyncDisposable
    {
        private readonly IConsumer<string, string> _consumer;
        private readonly ILogger _logger;
        private readonly ConsumerSettings _settings;
        private readonly Func<ConsumeResult<string, string>, Task> _messageHandler;
        private readonly IConsumerDeliveryStrategy _consumerDeliveryStrategy;
        private readonly TimeSpan _errorBackoff;
        // timeout to wait for graceful stop; can be overridden by settings if desired
        private readonly TimeSpan _gracefulStopTimeout;

        private CancellationTokenSource? _cts;
        private Task? _consumerTask;
        private int _started; // 0 = not started, 1 = started
        private int _stopping; // 0 = not stopping, 1 = stopping
        private bool _disposed;


        public KafkaConsumer(
            ConsumerSettings settings,
            ILogger logger,
            string topic,
            Func<ConsumeResult<string, string>, Task> messageHandler,
            IConsumerDeliveryStrategy? deliveryStrategy = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic must be provided", nameof(topic));

            _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));

            var config = ConfigureConsumer(_settings);

            _errorBackoff = _settings.RetryBackoffMs != default
                ? _settings.RetryBackoffMs : TimeSpan.FromMilliseconds(1000);

            // graceful stop timeout - if settings exposed a value, prefer it; otherwise use 30s
            _gracefulStopTimeout = _settings.HealthCheckTimeout ?? TimeSpan.FromSeconds(30);

            // allow injection of strategy, fallback to factory using settings.DeliverySemantics
            // Let the delivery strategy configure consumer-specific settings
            _consumerDeliveryStrategy = deliveryStrategy
                ?? DeliveryStrategyFactory.CreateConsumerStrategy(_settings.DeliverySemantics, _settings);
            
            _consumerDeliveryStrategy.ConfigureConsumerConfig(config, _settings);

            _consumer = new ConsumerBuilder<string, string>(config)
                .SetErrorHandler((_, e) => _logger.Error("Kafka Consumer error: {Reason}", e.Reason))
                .SetLogHandler((_, m) => _logger.Debug("Kafka Consumer log: {Facility} {Message}", m.Facility, m.Message))
                .SetPartitionsAssignedHandler((c, partitions) =>
                {
                    _logger.Information("Partitions assigned: {Partitions}", string.Join(",", partitions.Select(p => $"{p.Topic}:{p.Partition}")));
                    c.Assign(partitions);
                })
                .SetPartitionsRevokedHandler((c, partitions) =>
                {
                    _logger.Information("Partitions revoked: {Partitions}", string.Join(",", partitions.Select(p => $"{p.Topic}:{p.Partition}")));
                    c.Unassign();
                })
                .Build();

            _consumer.Subscribe(topic);
        }

        private ConsumerConfig ConfigureConsumer(ConsumerSettings settings)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = settings.BootstrapServers,
                GroupId = settings.GroupId,
                ClientId = settings.ClientId,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                // optimize for high throughput
                FetchMaxBytes = settings.MaxFetchBytes,
                MaxPartitionFetchBytes = settings.MaxPartitionFetchBytes
            };

            if (settings.ConsumerConfig != null)
            {
                foreach (var kv in settings.ConsumerConfig)
                    config.Set(kv.Key, kv.Value);
            }

            return config;
        }

        /// <summary>
        /// Starts the consumer background loop. Idempotent.
        /// </summary>
        public void Start()
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
            {
                _logger.Debug("Start called but consumer already started.");
                return;
            }

            _cts = new CancellationTokenSource();
            // run the processing loop on the thread pool
            _consumerTask = Task.Run(() => ConsumeProcessAsync(_cts.Token), CancellationToken.None);
            _logger.Information("Kafka consumer started.");
        }

        /// <summary>
        /// Stops the consumer gracefully, waiting up to configured timeout.
        /// </summary>
        public async Task StopAsync()
        {
            if (Interlocked.Exchange(ref _stopping, 1) == 1)
            {
                _logger.Debug("StopAsync called but stop already in progress.");
                if (_consumerTask != null)
                    await _consumerTask.ConfigureAwait(false);
                return;
            }

            try
            {
                if (_cts == null)
                {
                    _logger.Debug("StopAsync called but consumer was not started.");
                    return;
                }

                _cts.Cancel();

                if (_consumerTask != null)
                {
                    var completed = await Task.WhenAny(_consumerTask, Task.Delay(_gracefulStopTimeout)).ConfigureAwait(false);
                    if (completed != _consumerTask)
                    {
                        _logger.Warning("Consumer did not stop within {Timeout}. Forcing Close.", _gracefulStopTimeout);
                        try
                        {
                            _consumer.Close();
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning(ex, "Error during forced Close");
                        }
                    }
                    else
                    {
                        // ensure any exceptions are observed
                        await _consumerTask.ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error while stopping consumer");
            }
            finally
            {
                // consumer.Close() may be called already; calling again is safe but guard exceptions
                try
                {
                    _consumer.Close();
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Close during StopAsync threw");
                }

                // dispose cancellation source and reset start flag so the instance is not reusable
                try
                {
                    _cts.Dispose();
                }
                catch { /* ignore */ }

                _cts = null;
            }
        }

        private async Task ConsumeProcessAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(token);
                    if (result == null) continue;

                    try
                    {
                        await _messageHandler(result).ConfigureAwait(false);
                        // Let the strategy decide how to handle post-processing (commit/none)
                        await _consumerDeliveryStrategy.HandleAfterProcessAsync(_consumer, result).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Message handler failed for topic {Topic} partition {Partition} offset {Offset}",
                            result.Topic, result.Partition, result.Offset);
                        // TODO: implement DLQ publishing
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // expected during shutdown
                    break;
                }
                catch (ConsumeException ex)
                {
                    _logger.Error(ex, "Consume exception");
                    try
                    {
                        await Task.Delay(_errorBackoff, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unexpected consumer error");
                    try
                    {
                        await Task.Delay(_errorBackoff, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Async dispose to allow awaiting graceful shutdown.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                // DisposeAsync - waits for graceful stop (up to _gracefulStopTimeout)
                await StopAsync().ConfigureAwait(false);
            }
            finally
            {
                // Let Dispose() handle the actual resource cleanup
                Dispose();
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Synchronous dispose. Attempts a graceful stop, but will not block indefinitely.
        /// Prefer using StopAsync or DisposeAsync for graceful async shutdown.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                // Signal shutdown if not already stopping
                if (Interlocked.Exchange(ref _stopping, 1) == 0)
                {
                    _cts?.Cancel();
                }

                // Wait briefly for consumer task to complete
                if (_consumerTask != null && !_consumerTask.IsCompleted)
                {
                    _consumerTask.Wait(5000); // 5 second timeout, avoid TimeSpan allocation
                }

                // Close and dispose consumer resources
                _consumer.Close();
                _consumer.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error during Kafka consumer disposal");
            }
            finally
            {
                // Always clean up cancellation token source
                _cts?.Dispose();
                GC.SuppressFinalize(this);
            }
        }
    }
}
