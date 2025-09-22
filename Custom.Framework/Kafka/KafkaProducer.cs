using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Diagnostics;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Generic Kafka producer implementation that handles publishing messages to Kafka topics
    /// with support for metrics, distributed tracing, batch operations and delivery guarantees.
    /// </summary>
    public class KafkaProducer : IKafkaProducer, IDisposable
    {
        private readonly ILogger _logger;
        private readonly KafkaMetrics _metrics;
        private readonly ProducerOptions _options;
        private readonly ActivitySource _activitySource;
        private readonly IProducer<string, string> _producer;
        private readonly SemaphoreSlim _producerLock = new(1, 1);
        private readonly IProducer<string, string>? _transactionalProducer;
        private readonly IProducerDeliveryStrategy _deliveryStrategy;

        public KafkaProducer(ProducerOptions options, ILogger logger)
        {
            _logger = logger;
            _options = options;
            _metrics = new KafkaMetrics(options.ClientId);
            _activitySource = new ActivitySource("Kafka.Producer");

            var producerConfig = CreateProducerConfig(options);
            _producer = CreateProducer(producerConfig);

            // initialize strategy early so CreateProducerConfig can use it
            _deliveryStrategy = DeliveryStrategyFactory.CreateProducerStrategy(_producer, producerConfig, options);

            if (_deliveryStrategy.RequiresTransactionalProducer)
            {
                var transactionalConfig = CreateTransactionalProducerConfig(options);
                _transactionalProducer = CreateProducer(transactionalConfig);
            }
        }

        /// <summary>
        /// ctor with configuration support by DeliverySemantics enum
        /// </summary>
        public KafkaProducer(DeliverySemantics deliverySemantics, ILogger logger, IConfiguration configuration)
        {
            var options = configuration.GetSection($"Kafka:Producer:{deliverySemantics}").Get<ProducerOptions>()
                ?? throw new NotImplementedException($"Kafka:Producer:{deliverySemantics} not found in appsettings.[environment].json"); ;
            _logger = logger;
            _metrics = new KafkaMetrics(options.ClientId);
            _activitySource = new ActivitySource("Kafka.Producer");

            var producerConfig = CreateProducerConfig(options);
            _producer = CreateProducer(producerConfig);

            // initialize strategy early so CreateProducerConfig can use it
            _deliveryStrategy = DeliveryStrategyFactory.CreateProducerStrategy(_producer, producerConfig, options);

            if (_deliveryStrategy.RequiresTransactionalProducer)
            {
                var transactionalConfig = CreateTransactionalProducerConfig(options);
                _transactionalProducer = CreateProducer(transactionalConfig);
            }
        }

        /// <summary>
        /// Produces a single message to a Kafka topic with configurable delivery semantics, tracing and metrics.
        /// </summary>
        public async Task PublishAsync(
            KafkaMessage message,
            int attemptCount = 1,
            CancellationToken cancellationToken = default)
        {
            using var activity = _activitySource.StartActivity("KafkaProducer.Produce");
            var stopwatch = DateTime.Now.Ticks;

            try
            {
                await _producerLock.WaitAsync(cancellationToken);

                message.Headers = CreateMessageHeaders(message.Key);

                // Use strategy to produce (no switch-case)
                var delivery = await _deliveryStrategy.PublishAsync(
                    message.Topic, message, _producer, _transactionalProducer, cancellationToken)
                    .ConfigureAwait(false);

                RecordMetrics(TimeSpan.FromTicks(DateTime.Now.Ticks - stopwatch), delivery, activity);
            }
            catch (Exception ex)
            {
                HandleProduceException(ex, message.Topic, activity);
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
        public async Task PublishBatchAsync(
            string topic,
            IEnumerable<KafkaMessage> messages,
            CancellationToken cancellationToken = default)
        {
            using var activity = _activitySource.StartActivity("KafkaProducer.ProduceBatch");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _producerLock.WaitAsync(cancellationToken);

                var tasks = new List<Task<DeliveryResult<string, string>>>();
                foreach (var message in messages)
                {
                    var headers = CreateMessageHeaders(message.Key);
                    message.Headers = headers;
                    message.Value = JsonConvert.SerializeObject(message.Value);

                    // delegate to strategy for each message
                    tasks.Add(_deliveryStrategy.PublishAsync(topic, message,
                            _producer, _transactionalProducer, cancellationToken));
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

        private ProducerConfig CreateProducerConfig(ProducerOptions options)
        {
            var config = new ProducerConfig
            {
                BootstrapServers = options.BootstrapServers,
                ClientId = options.ClientId,
                LingerMs = options.LingerMs,
                BatchSize = options.BatchSize,
                MessageMaxBytes = options.MessageMaxBytes,
                CompressionType = (CompressionType)Enum.Parse(typeof(CompressionType), options.CompressionType ?? "None"),
                CompressionLevel = options.CompressionLevel,
                RetryBackoffMs = options.RetryBackoffMs,
            };

            // Let the strategy configure delivery-specific producer options
            //_deliveryStrategy.ConfigureProducerConfig(producerConfig, _options);

            ConfigureSecurity(config, options);

            if (options.ProducerConfig != null)
            {
                foreach (var kv in options.ProducerConfig)
                    config.Set(kv.Key, kv.Value);
            }

            return config;
        }

        private ProducerConfig CreateTransactionalProducerConfig(ProducerOptions options)
        {
            var config = CreateProducerConfig(options);
            config.TransactionalId = $"{options.ClientId}-{Guid.NewGuid()}";
            config.TransactionTimeoutMs = options.TransactionTimeoutMs ?? 60000;
            config.EnableIdempotence = true;
            return config;
        }

        private void ConfigureSecurity(ProducerConfig config, ProducerOptions options)
        {
            if (!string.IsNullOrEmpty(options.SaslUsername))
            {
                config.SaslUsername = options.SaslUsername;
                config.SaslPassword = options.SaslPassword;
                config.SaslMechanism = (SaslMechanism)Enum.Parse(typeof(SaslMechanism), options.SaslMechanism ?? "Plain");
                config.SecurityProtocol = (SecurityProtocol)Enum.Parse(typeof(SecurityProtocol), options.SecurityProtocol ?? "SaslSsl");
            }
        }

        private IProducer<string, string> CreateProducer(ProducerConfig config)
        {
            //var config = CreateProducerConfig(options);
            return new ProducerBuilder<string, string>(config)
                .SetErrorHandler((_, e) =>
                {
                    _metrics.RecordError();
                    _logger.Error("Kafka Producer error: {Reason}", e.Reason);
                })
                .SetLogHandler((_, m) => _logger.Debug("Kafka Producer log: {Facility} {Message}", m.Facility, m.Message))
                .SetStatisticsHandler((_, json) =>
                {
                    //if (_options.EnableMetrics)
                        //_logger.Debug("Kafka Producer stats: {Stats}", json);
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
