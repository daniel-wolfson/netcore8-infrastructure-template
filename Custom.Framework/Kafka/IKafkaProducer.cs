using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Interface for producing messages to Kafka topics
    /// </summary>
    public interface IKafkaProducer
    {
        /// <summary>
        /// Produces a single message to a Kafka topic
        /// </summary>
        Task PublishAsync(KafkaMessage message, int attemptCount = 1,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Produces multiple messages to a Kafka topic in batch
        /// </summary>
        Task PublishBatchAsync(string topic, IEnumerable<KafkaMessage> record, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Flushes any pending messages
        /// </summary>
        Task FlushAsync(TimeSpan timeout);
    }
}