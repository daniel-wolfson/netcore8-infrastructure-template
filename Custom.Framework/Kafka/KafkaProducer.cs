using Confluent.Kafka;
using Newtonsoft.Json;
using System.Diagnostics;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Generic Kafka producer implementation that handles publishing messages to Kafka topics
    /// with support for metrics, distributed tracing, batch operations and delivery guarantees.
    /// </summary>
    public class KafkaProducer<TMessage> : IKafkaProducer<TMessage>, IDisposable
    {
        private readonly ILogger _logger;
        private readonly ProducerSettings _settings;
        private readonly KafkaMetrics _metrics;
        private readonly ActivitySource _activitySource;
        private readonly IProducer<string, string> _producer;
        private readonly SemaphoreSlim _producerLock = new(1, 1);
        private readonly IProducer<string, string>? _transactionalProducer;
        private readonly IProducerDeliveryStrategy _deliveryStrategy;

        public KafkaProducer(ProducerSettings settings, ILogger logger, IProducerDeliveryStrategy? deliveryStrategy = null)
        {
            _settings = settings;
            _logger = logger;
            _metrics = new KafkaMetrics(settings.ClientId);
            _activitySource = new ActivitySource("Kafka.Producer");

            // initialize strategy early so CreateProducerConfig can use it
            _deliveryStrategy = deliveryStrategy ?? DeliveryStrategyFactory.CreateProducerStrategy(_settings.DeliverySemantics, _settings);

            var config = CreateProducerConfig();
            _producer = CreateProducer(config);

            if (_deliveryStrategy.RequiresTransactionalProducer)
            {
                var transactionalConfig = CreateTransactionalProducerConfig();
                _transactionalProducer = CreateProducer(transactionalConfig);
            }
        }

        /// <summary>
        /// Produces a single message to a Kafka topic with configurable delivery semantics, tracing and metrics.
        /// </summary>
        public async Task ProduceAsync(string topic, TMessage message, string? key = null, string? correlationId = null, CancellationToken cancellationToken = default)
        {
            using var activity = _activitySource.StartActivity("KafkaProducer.Produce");
            var stopwatch = DateTime.Now.Ticks;

            try
            {
                await _producerLock.WaitAsync(cancellationToken);

                var value = JsonConvert.SerializeObject(message);
                var headers = CreateMessageHeaders(correlationId);

                var msg = new Message<string, string>
                {
                    Key = key ?? Guid.NewGuid().ToString(),
                    Value = value,
                    Headers = headers
                };

                // Use strategy to produce (no switch-case)
                var delivery = await _deliveryStrategy.ProduceAsync(_producer, _transactionalProducer, topic, msg, cancellationToken).ConfigureAwait(false);

                RecordMetrics(TimeSpan.FromTicks(DateTime.Now.Ticks - stopwatch), delivery, activity);
            }
            catch (Exception ex)
            {
                HandleProduceException(ex, topic, activity);
                throw;
            }
            finally
            {
                _producerLock.Release();
            }
        }

        /// <summary>
        /// Produces multiple messages to a Kafka topic in batch with configurable delivery semantics, tracing and metrics.
        /// </summary>
        public async Task ProduceBatchAsync(
            string topic,
            IEnumerable<(TMessage Message, string? Key)> messages,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            using var activity = _activitySource.StartActivity("KafkaProducer.ProduceBatch");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _producerLock.WaitAsync(cancellationToken);

                var tasks = new List<Task<DeliveryResult<string, string>>>();
                foreach (var (message, key) in messages)
                {
                    var value = JsonConvert.SerializeObject(message);
                    var headers = CreateMessageHeaders(correlationId);
                    var msg = new Message<string, string>
                    {
                        Key = key ?? Guid.NewGuid().ToString(),
                        Value = value,
                        Headers = headers
                    };

                    // delegate to strategy for each message
                    tasks.Add(_deliveryStrategy.ProduceAsync(_producer, _transactionalProducer, topic, msg, cancellationToken));
                }

                var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                foreach (var delivery in results)
                {
                    RecordMetrics(stopwatch.Elapsed, delivery, activity);
                }
            }
            catch (Exception ex)
            {
                HandleProduceException(ex, topic, activity);
                throw;
            }
            finally
            {
                _producerLock.Release();
            }
        }

        public Task FlushAsync(TimeSpan timeout)
        {
            return Task.Run(() =>
            {
                _producer.Flush(timeout);
                _transactionalProducer?.Flush(timeout);
            });
        }

        public void Dispose()
        {
            try
            {
                _producerLock.Dispose();
                _producer.Flush(TimeSpan.FromSeconds(5));
                _producer.Dispose();

                if (_transactionalProducer != null)
                {
                    _transactionalProducer.Flush(TimeSpan.FromSeconds(5));
                    _transactionalProducer.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error while disposing Kafka producer");
            }
        }

        #region private methods

        private ProducerConfig CreateProducerConfig()
        {
            var config = new ProducerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                ClientId = _settings.ClientId,
                LingerMs = _settings.LingerMs,
                BatchSize = _settings.BatchSize,
                MessageMaxBytes = _settings.MessageMaxBytes,
                CompressionType = (CompressionType)Enum.Parse(typeof(CompressionType), _settings.CompressionType ?? "None"),
                CompressionLevel = _settings.CompressionLevel,
                RetryBackoffMs = (int)_settings.RetryBackoffMs.TotalMilliseconds,
            };

            // Let the strategy configure delivery-specific producer settings
            _deliveryStrategy.ConfigureProducerConfig(config, _settings);
            ConfigureSecurity(config);

            if (_settings.ProducerConfig != null)
            {
                foreach (var kv in _settings.ProducerConfig)
                    config.Set(kv.Key, kv.Value);
            }

            return config;
        }

        private ProducerConfig CreateTransactionalProducerConfig()
        {
            var config = CreateProducerConfig();
            config.TransactionalId = $"{_settings.ClientId}-{Guid.NewGuid()}";
            config.TransactionTimeoutMs = _settings.TransactionTimeoutMs ?? 60000;
            config.EnableIdempotence = true;
            return config;
        }

        private void ConfigureSecurity(ProducerConfig config)
        {
            if (!string.IsNullOrEmpty(_settings.SaslUsername))
            {
                config.SaslUsername = _settings.SaslUsername;
                config.SaslPassword = _settings.SaslPassword;
                config.SaslMechanism = (SaslMechanism)Enum.Parse(typeof(SaslMechanism), _settings.SaslMechanism ?? "Plain");
                config.SecurityProtocol = (SecurityProtocol)Enum.Parse(typeof(SecurityProtocol), _settings.SecurityProtocol ?? "SaslSsl");
            }
        }

        private IProducer<string, string> CreateProducer(ProducerConfig config)
        {
            return new ProducerBuilder<string, string>(config)
                .SetErrorHandler((_, e) =>
                {
                    _metrics.RecordError();
                    _logger.Error("Kafka Producer error: {Reason}", e.Reason);
                })
                .SetLogHandler((_, m) => _logger.Debug("Kafka Producer log: {Facility} {Message}", m.Facility, m.Message))
                .SetStatisticsHandler((_, json) =>
                {
                    if (_settings.EnableMetrics)
                        _logger.Debug("Kafka Producer stats: {Stats}", json);
                })
                .Build();
        }

        private Headers CreateMessageHeaders(string? correlationId)
        {
            var headers = new Headers();

            if (Activity.Current != null)
            {
                headers.Add("trace-id", System.Text.Encoding.UTF8.GetBytes(Activity.Current.TraceId.ToString()));
                headers.Add("span-id", System.Text.Encoding.UTF8.GetBytes(Activity.Current.SpanId.ToString()));
            }

            if (!string.IsNullOrEmpty(correlationId))
                headers.Add("correlation-id", System.Text.Encoding.UTF8.GetBytes(correlationId));

            // Add timestamp for monitoring
            headers.Add("produced-at", System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()));

            return headers;
        }

        private void RecordMetrics(TimeSpan elapsed, DeliveryResult<string, string> delivery, Activity? activity)
        {
            _metrics.RecordMessageSent();
            _metrics.RecordProducerLatency(elapsed);

            activity?.SetTag("topic", delivery.Topic);
            activity?.SetTag("partition", delivery.Partition.Value);
            activity?.SetTag("offset", delivery.Offset.Value);
            activity?.SetTag("messageKey", delivery.Message.Key);
            activity?.SetTag("deliveryLatencyMs", elapsed.TotalMilliseconds);

            _logger.Information(
                "Produced message to {TopicPartitionOffset} | Key: {Key} | Latency: {Latency}ms",
                delivery.TopicPartitionOffset, delivery.Message.Key, elapsed.TotalMilliseconds);
        }

        private void HandleProduceException(Exception ex, string topic, Activity? activity)
        {
            _metrics.RecordError();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            if (ex is ProduceException<string, string> produceEx)
            {
                _logger.Error(produceEx,
                    "Failed to produce message to topic {Topic}. Error: {Error} | IsFatal: {IsFatal}",
                    topic, produceEx.Error.Reason, produceEx.Error.IsFatal);
            }
            else
            {
                _logger.Error(ex, "Unexpected error producing message to topic {Topic}", topic);
            }
        }

        #endregion private methods
    }
}
