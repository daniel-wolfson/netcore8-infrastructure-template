using Confluent.Kafka;
using Custom.Domain.Optima.Models.Main;
using Custom.Framework.Helpers;
using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using static Confluent.Kafka.ConfigPropertyNames;

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
        private readonly KafkaOptions _options;
        private readonly ActivitySource _activitySource;
        private bool _disposed;

        private readonly IProducer<string, byte[]> _producer;
        private readonly IProducer<string, byte[]>? _transactionalProducer;
        private readonly IProducerDeliveryStrategy _deliveryStrategy;
        public string[] Topics => [.. _options.Producers.SelectMany(x => x.Topics)];

        public KafkaProducer(string producerName, KafkaOptions options, 
            IProducerDeliveryStrategy deliveryStrategy, ILogger logger)
        {
            _logger = logger;
            _options = options;
            var clientId = !string.IsNullOrEmpty(producerName)
                ? $"{_options.Common.ServiceShortName}-{producerName}-producer"
                : $"{_options.Common.ServiceShortName}-producer";
            _metrics = new KafkaMetrics(clientId);
            _activitySource = new ActivitySource(nameof(KafkaProducer));

            var deliverySemantics = _options.Producers
                .FirstOrDefault(p => p.Name.Equals(producerName, StringComparison.OrdinalIgnoreCase))
                ?.DeliverySemantics ?? DeliverySemantics.AtLeastOnce;

            var producerSettings = _options.Producers
                .FirstOrDefault(p => p.Name.Equals(producerName, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Producer settings not found for name: {producerName}");

            _deliveryStrategy = deliveryStrategy
                ?? DeliveryStrategyFactory.CreateProducerStrategy(deliverySemantics, producerSettings);

            _producer = CreateProducer(_deliveryStrategy.ProducerConfig);

            if (_deliveryStrategy.RequiresTransactionalProducer)
            {
                var transactionalConfig = CreateTransactionalProducerConfig(producerName, clientId, producerSettings);
                _transactionalProducer = CreateProducer(transactionalConfig);
            }
        }

        /// <summary>
        /// Produces a single producerMessage to a Kafka topic with configurable delivery semantics, tracing and metrics.
        /// </summary>
        public async Task PublishAsync<TMessage>(string topic, TMessage message, CancellationToken cancellationToken = default)
        {
            Activity? activity = null;
            long stopwatch = DateTime.Now.Ticks;

            var settings = _options.Producers.FirstOrDefault(x => x.Topics.Contains(topic))
                ?? throw new ArgumentException($"Topic '{topic}' is not configured for any producer.");

            try
            {
                activity = _activitySource.StartActivity("KafkaProducer.Produce");

                var messageKey = Guid.NewGuid().ToString();
                var producerMessage = new Message<string, byte[]>
                {
                    Key = messageKey,
                    Headers = CreateMessageHeaders(settings, Guid.NewGuid().ToString(), messageKey),
                    Value = JsonSerializer.SerializeToUtf8Bytes(message)
                };

                // Use strategy to produce (no switch-case)
                var delivery = await _deliveryStrategy.PublishAsync(
                    topic, producerMessage, _producer, _transactionalProducer, cancellationToken)
                    .ConfigureAwait(false);

                RecordMetrics(TimeSpan.FromTicks(DateTime.Now.Ticks - stopwatch), delivery, activity);
            }
            catch (Exception ex)
            {
                HandleProduceException(ex, topic, activity);
                throw;
            }
        }

        /// <summary>
        /// Publishes a batch of messages asynchronously to the specified topic.
        /// Each message is sent individually to avoid creating JSON arrays.
        /// </summary>
        public async Task PublishAllAsync<TMessage>(string topic, IEnumerable<TMessage> messages, CancellationToken cancellationToken = default)
        {
            var publishTasks = new List<Task>();
            foreach (var message in messages)
            {
                publishTasks.Add(PublishAsync(topic, message, cancellationToken));
            }
            await Task.WhenAll(publishTasks).ConfigureAwait(false);

            int successCount = 0;
            int failureCount = 0;

            foreach (var task in publishTasks)
            {
                if (task.IsFaulted)
                {
                    failureCount++;
                    _logger.Error(task.Exception, "{Title} Individual message publish failed", ApiHelper.LogTitle());
                }
                else if (task.IsCompletedSuccessfully)
                {
                    successCount++;
                }
            }

            _logger.Information("{Title} Batch publish completed. {SuccessCount} successful, {FailureCount} failed",
                ApiHelper.LogTitle(), successCount, failureCount);
        }

        public async Task PublishToDeadLettersAsync<TMessage>(string topic,
            TMessage message,
            Exception? exception = null,
            int attemptCount = 1,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Create dead letter deliveryResultSource with error context
                //var deadLetterPayload = CreateDeadLetterMessage(deliveryResultSource, exception, attemptCount);

                // Preserve original key for partitioning consistency
                //var originalMessage = message;
                //var dlq_mssage = new Message<string, byte[]>
                //{
                //    Key = originalMessage.Key,
                //    Value = originalMessage.Value,
                //    Headers = CreateDeadLetterHeaders(originalMessage, exception, attemptCount)
                //};

                //var deliveryResult2 = await _producer.ProduceAsync(topic, originalMessage, cancellationToken);

                //_logger.Information("Message published to DLQ: Name={DlqTopic} Partition={Partition} Offset={Offset} OriginalTopic={OriginalTopic}",
                //    topic, deliveryResultSource.Partition.Value, deliveryResultSource.Offset.Value, topic);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"{ApiHelper.LogTitle()} Failed to publish to dead letter", topic);
                // Don't rethrow - DLQ failure shouldn't stop deliveryResultSource processing
            }
        }

        /// <summary>
        /// Produces multiple messages to a Kafka topic in batch with configurable delivery semantics, tracing and metrics.
        /// </summary>
        public async Task PublishBatchAsync<TMessage>(string topic, IEnumerable<TMessage> messages, CancellationToken cancellationToken = default)
        {
            using var activity = _activitySource.StartActivity("KafkaProducer.ProduceBatch");
            var stopwatchTicks = DateTimeOffset.UtcNow.Ticks;
            ProducerSettings? settings;
            if (_options.Producers.Exists(x => x.Topics.Contains(topic)))
            {
                settings = _options.Producers.FirstOrDefault(x => x.Topics.Contains(topic));
            }
            else
            {
                throw new ArgumentException($"Topic '{topic}' is not configured for any producer.");
            }

            try
            {
                var tasks = new List<Task<DeliveryResult<string, byte[]>>>();
                var correlationId = Guid.NewGuid().ToString();
                var messageKey = Guid.NewGuid().ToString();
                var headers = CreateMessageHeaders(settings, correlationId, messageKey);
                var publishMessage = new Message<string, byte[]>
                {
                    Key = messageKey,
                    Value = JsonSerializer.SerializeToUtf8Bytes(messages),
                    Headers = headers
                };

                // delegate to strategy for each producerMessage
                tasks.Add(
                    _deliveryStrategy.PublishAsync(topic, publishMessage,
                        _producer, _transactionalProducer, cancellationToken));

                var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                foreach (var delivery in results)
                {
                    RecordMetrics(
                        TimeSpan.FromTicks(DateTimeOffset.UtcNow.Ticks - stopwatchTicks),
                        delivery, activity);
                }
            }
            catch (Exception ex)
            {
                HandleProduceException(ex, topic, activity);
                throw;
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
            if (_disposed) return;
            _disposed = true;

            try
            {
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

        private ProducerConfig CreateTransactionalProducerConfig(
            string producerName, string clientId, ProducerSettings producerSettings)
        {
            var config = _deliveryStrategy.ProducerConfig;
            config.TransactionalId = $"{clientId}-{Guid.NewGuid()}";
            config.TransactionTimeoutMs = producerSettings.TransactionTimeoutMs
                ?? Timeouts.TransactionTimeoutMs.Milliseconds;
            config.EnableIdempotence = true;
            return config;
        }

        private IProducer<string, byte[]> CreateProducer(ProducerConfig config)
        {
            //var config = CreateProducerConfig(settings);
            return new ProducerBuilder<string, byte[]>(config)
                //.SetValueSerializer(new KafkaMessageSerializer<TMessage>())
                .SetErrorHandler((_, e) =>
                {
                    _metrics.RecordError();
                    _logger.Error("Kafka Producer error: {Reason}", e.Reason);
                })
                .SetLogHandler((_, m) => _logger.Debug("Kafka Producer log: {Facility} {Message}", m.Facility, m.Message))
                .SetStatisticsHandler((_, json) =>
                {
                    //if (_kafkaOptions.EnableMetrics)
                    //_logger.Debug("Kafka Producer stats: {Stats}", json);
                })
                .Build();
        }

        private Headers CreateMessageHeaders(ProducerSettings settings, string? correlationId, string? messageKey = null)
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

            // Add duplicate detection support if enabled
            if (settings.EnableDuplicateDetection)
            {
                // Use message key + timestamp as idempotence key for duplicate detection
                var idempotenceKey = !string.IsNullOrEmpty(messageKey)
                    ? $"{messageKey}-{DateTimeOffset.UtcNow.Ticks}"
                    : Guid.NewGuid().ToString();

                headers.Add("idempotence-key", System.Text.Encoding.UTF8.GetBytes(idempotenceKey));
                headers.Add("message-id", System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
            }

            return headers;
        }

        private void RecordMetrics(TimeSpan elapsed, DeliveryResult<string, byte[]> delivery, Activity? activity)
        {
            _metrics.RecordMessageSent();
            _metrics.RecordProducerLatency(elapsed);

            activity?.SetTag("topic", delivery.Topic);
            activity?.SetTag("partition", delivery.Partition.Value);
            activity?.SetTag("offset", delivery.Offset.Value);
            activity?.SetTag("messageKey", delivery.Message.Key);
            activity?.SetTag("deliveryLatencyMs", elapsed.TotalMilliseconds);

            _logger.Information(
                "Produced producerMessage to {TopicPartitionOffset} | Key: {Key} | Latency: {Latency}ms",
                delivery.TopicPartitionOffset, delivery.Message.Key, elapsed.TotalMilliseconds);
        }

        private void HandleProduceException(Exception ex, string topic, Activity? activity)
        {
            _metrics.RecordError();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            if (ex is ProduceException<string, string> produceEx)
            {
                _logger.Error(produceEx,
                    "Failed to produce producerMessage to topic {Name}. Error: {Error} | IsFatal: {IsFatal}",
                    topic, produceEx.Error.Reason, produceEx.Error.IsFatal);
            }
            else
            {
                _logger.Error(ex, "Unexpected error producing producerMessage to topic {Name}", topic);
            }
        }

        private KafkaDeadLetterMessage<byte[]> CreateDeadLetterMessage(ConsumeResult<string, byte[]> result, Exception? exception, int attemptCount)
        {
            return new KafkaDeadLetterMessage<byte[]>
            {
                OriginalTopic = result.Topic,
                OriginalPartition = result.Partition.Value,
                OriginalOffset = result.Offset.Value,
                OriginalKey = result.Message.Key,
                //OriginalValue = result.Message.Value,
                OriginalTimestamp = result.Message.Timestamp.UtcDateTime,
                FailureInfo = exception != null
                    ? new FailureInfo
                    {
                        ErrorType = exception?.GetType().Name ?? string.Empty,
                        ErrorMessage = exception?.Message ?? string.Empty,
                        StackTrace = exception?.StackTrace ?? string.Empty,
                        AttemptCount = attemptCount,
                        FailedAt = DateTime.UtcNow
                    }
                    : null
            };
        }

        private Headers CreateDeadLetterHeaders(Message<string, byte[]> originalMessage, Exception? exception, int attemptCount)
        {
            var headers = new Headers();

            // Copy original headers
            if (originalMessage.Headers != null)
            {
                foreach (var header in originalMessage.Headers)
                {
                    headers.Add($"dlq-original-{header.Key}", header.GetValueBytes());
                }
            }

            // Add DLQ-specific headers
            //headers.Add("dlq-original-topic", System.Text.Encoding.UTF8.GetBytes(originalMessage.Name));
            //headers.Add("dlq-original-partition", BitConverter.GetBytes(originalMessage.Partition.Value));
            //headers.Add("dlq-original-offset", BitConverter.GetBytes(originalMessage.Offset.Value));
            headers.Add("dlq-error-type", System.Text.Encoding.UTF8.GetBytes(exception?.GetType().Name ?? ""));
            headers.Add("dlq-attempt-count", BitConverter.GetBytes(attemptCount));
            headers.Add("dlq-failed-at", BitConverter.GetBytes(DateTime.UtcNow.ToBinary()));

            return headers;
        }

        #endregion private methods
    }
}
