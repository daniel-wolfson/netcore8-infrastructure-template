using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Interface for producing messages to Kafka topics
    /// </summary>
    public interface IKafkaProducer : IDisposable
    {
        string[] Topics { get; }

        /// <summary>
        /// Produces a single message to a Kafka topic
        /// </summary>
        Task PublishAsync<TMessage>(string topic, TMessage message, CancellationToken cancellationToken = default);

        Task PublishAllAsync<TMessage>(string topic, IEnumerable<TMessage> messages, CancellationToken cancellationToken = default);

        /// <summary>
        /// Produces multiple messages to a Kafka topic in batch
        /// </summary>
        Task PublishBatchAsync<TMessage>(string topic, IEnumerable<TMessage> messages, CancellationToken cancellationToken = default);

        Task PublishToDeadLettersAsync<TMessage>(string topic, TMessage message,
            Exception? exception = null, int attemptCount = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Flushes any pending messages
        /// </summary>
        Task FlushAsync(TimeSpan timeout);
    }

    public interface IKafkaProducer<TMessage>
    {
        /// <summary>
        /// Produces a single message to a Kafka topic
        /// </summary>
        Task PublishAsync(TMessage message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Produces multiple messages to a Kafka topic in batch
        /// </summary>
        Task PublishBatchAsync(string topic, IEnumerable<TMessage> messages, CancellationToken cancellationToken = default);

        Task PublishToDeadLetterAsync(string topic, ConsumeResult<string, TMessage> deliveryResultSource,
            Exception? exception = null, int attemptCount = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Flushes any pending messages
        /// </summary>
        Task FlushAsync(TimeSpan timeout);
    }


}