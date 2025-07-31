namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Interface for producing messages to Kafka topics
    /// </summary>
    /// <typeparam name="TMessage">The type of message to be produced</typeparam>
    public interface IKafkaProducer<TMessage>
    {
        /// <summary>
        /// Produces a single message to a Kafka topic
        /// </summary>
        Task ProduceAsync(string topic, TMessage message, string? key = null, string? correlationId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Produces multiple messages to a Kafka topic in batch
        /// </summary>
        Task ProduceBatchAsync(string topic, IEnumerable<(TMessage Message, string? Key)> messages, string? correlationId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Flushes any pending messages
        /// </summary>
        Task FlushAsync(TimeSpan timeout);
    }
}