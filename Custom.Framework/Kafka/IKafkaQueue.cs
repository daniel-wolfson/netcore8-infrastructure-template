using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Interface for publishing messages to a dead letter queue when processing fails permanently.
    /// </summary>
    public interface IKafkaQueue
    {
        /// <summary>
        /// Publishes a failed message to the dead letter queue with error context.
        /// </summary>
        /// <param name="message">The original Kafka message that failed</param>
        /// <param name="exception">The exception that caused the failure</param>
        /// <param name="attemptCount">Number of processing attempts made</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task PublishAsync(KafkaMessage message, Exception exception, int attemptCount,
            CancellationToken cancellationToken = default);
    }
}