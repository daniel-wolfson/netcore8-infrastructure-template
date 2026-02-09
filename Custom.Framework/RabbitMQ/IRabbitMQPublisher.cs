using RabbitMQ.Client;

namespace Custom.Framework.RabbitMQ;

/// <summary>
/// Interface for publishing messages to RabbitMQ exchanges
/// Optimized for high-throughput scenarios (10k+ msg/sec)
/// </summary>
public interface IRabbitMQPublisher : IDisposable
{
    /// <summary>
    /// Publish a single message to an exchange
    /// </summary>
    Task PublishAsync<TMessage>(string exchange, string routingKey, TMessage message, 
        CancellationToken cancellationToken = default) where TMessage : class;

    /// <summary>
    /// Publish multiple messages in batch (optimized for throughput)
    /// </summary>
    Task PublishBatchAsync<TMessage>(string exchange, string routingKey, IEnumerable<TMessage> messages, 
        CancellationToken cancellationToken = default) where TMessage : class;

    /// <summary>
    /// Publish message with custom properties
    /// </summary>
    Task PublishAsync<TMessage>(string exchange, string routingKey, TMessage message, 
        IBasicProperties? properties, CancellationToken cancellationToken = default) where TMessage : class;

    /// <summary>
    /// Publish to dead letter exchange
    /// </summary>
    Task PublishToDeadLetterAsync<TMessage>(string originalExchange, string originalRoutingKey, 
        TMessage message, Exception? exception = null, int attemptCount = 1, 
        CancellationToken cancellationToken = default) where TMessage : class;

    /// <summary>
    /// Flush any pending messages (wait for confirms if enabled)
    /// </summary>
    Task FlushAsync(TimeSpan timeout);

    /// <summary>
    /// Check if publisher is healthy
    /// </summary>
    bool IsHealthy();
}

/// <summary>
/// Generic publisher for specific message type
/// </summary>
public interface IRabbitMQPublisher<TMessage> : IDisposable where TMessage : class
{
    /// <summary>
    /// Publish a single message
    /// </summary>
    Task PublishAsync(TMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish multiple messages in batch
    /// </summary>
    Task PublishBatchAsync(IEnumerable<TMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish to dead letter queue
    /// </summary>
    Task PublishToDeadLetterAsync(TMessage message, Exception? exception = null, 
        int attemptCount = 1, CancellationToken cancellationToken = default);
}
