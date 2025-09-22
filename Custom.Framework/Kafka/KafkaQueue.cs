using Confluent.Kafka;
using System.Text.Json;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Kafka-based dead letter queue implementation that publishes failed messages to a DLQ topic.
    /// </summary>
    public class KafkaQueue : IKafkaQueue, IDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger _logger;
        private readonly string _deadLetterTopicSuffix;
        private bool _disposed;

        public KafkaQueue(ProducerConfig config, ILogger logger, string deadLetterTopicSuffix = "-dlq")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _deadLetterTopicSuffix = deadLetterTopicSuffix;
            
            // Configure producer for DLQ with reliability settings
            var dlqConfig = new ProducerConfig(config)
            {
                EnableIdempotence = true,
                MessageTimeoutMs = 30000,
                RequestTimeoutMs = 10000,
                Acks = Acks.All
            };

            _producer = new ProducerBuilder<string, string>(dlqConfig)
                .SetErrorHandler((_, e) => _logger.Error("DLQ Producer error: {Reason}", e.Reason))
                .Build();
        }

        public async Task PublishAsync(
            KafkaMessage message, 
            Exception? exception = null, 
            int attemptCount = 1,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(ConsumeResult<,>));

            try
            {
                var deadLetterTopic = $"{message.Topic}{_deadLetterTopicSuffix}";
                
                // Create dead letter originalMessage with error context
                var deadLetterPayload = CreateDeadLetterMessage(message, exception, attemptCount);
                
                // Preserve original key for partitioning consistency
                var originalMessage = new Message<string, string>
                {
                    Key = message.Key,
                    Value = JsonSerializer.Serialize(deadLetterPayload),
                    Headers = CreateDeadLetterHeaders(message, exception, attemptCount)
                };

                var deliveryResult = await _producer.ProduceAsync(deadLetterTopic, originalMessage, cancellationToken);
                
                _logger.Information("Message published to DLQ: Topic={DlqTopic} Partition={Partition} Offset={Offset} OriginalTopic={OriginalTopic}",
                    deadLetterTopic, deliveryResult.Partition.Value, deliveryResult.Offset.Value, message.Topic);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to publish originalMessage to dead letter queue for topic {Topic} partition {Partition} offset {Offset}",
                    message.Topic, message.Partition.Value, message.Offset.Value);
                // Don't rethrow - DLQ failure shouldn't stop originalMessage processing
            }
        }

        private object CreateDeadLetterMessage(KafkaMessage originalMessage, Exception? exception, int attemptCount)
        {
            return new
            {
                OriginalTopic = originalMessage.Topic,
                OriginalPartition = originalMessage.Partition.Value,
                OriginalOffset = originalMessage.Offset.Value,
                OriginalKey = originalMessage.Key,
                OriginalValue = originalMessage.Value,
                OriginalTimestamp = originalMessage.Timestamp.UtcDateTime,
                FailureInfo = new
                {
                    ErrorType = exception?.GetType().Name,
                    ErrorMessage = exception?.Message,
                    StackTrace = exception?.StackTrace,
                    AttemptCount = attemptCount,
                    FailedAt = DateTime.UtcNow
                }
            };
        }

        private Headers CreateDeadLetterHeaders(KafkaMessage originalMessage, Exception? exception, int attemptCount)
        {
            var headers = new Headers();
            
            // Copy original headers
            if (originalMessage.Headers != null)
            {
                foreach (var header in originalMessage.Headers)
                {
                    headers.Add($"original-{header.Key}", header.GetValueBytes());
                }
            }
            
            // Add DLQ-specific headers
            headers.Add("dlq-original-topic", System.Text.Encoding.UTF8.GetBytes(originalMessage.Topic));
            headers.Add("dlq-original-partition", BitConverter.GetBytes(originalMessage.Partition.Value));
            headers.Add("dlq-original-offset", BitConverter.GetBytes(originalMessage.Offset.Value));
            headers.Add("dlq-error-type", System.Text.Encoding.UTF8.GetBytes(exception?.GetType().Name ?? ""));
            headers.Add("dlq-attempt-count", BitConverter.GetBytes(attemptCount));
            headers.Add("dlq-failed-at", BitConverter.GetBytes(DateTime.UtcNow.ToBinary()));
            
            return headers;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _producer?.Flush(TimeSpan.FromSeconds(10));
                _producer?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error disposing DLQ producer");
            }
        }
    }
}