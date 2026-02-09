namespace Custom.Framework.RabbitMQ;

/// <summary>
/// Interface for subscribing to RabbitMQ queues
/// Optimized for high-throughput scenarios (10k+ msg/sec)
/// </summary>
public interface IRabbitMQSubscriber : IDisposable
{
    /// <summary>
    /// Start consuming messages from a queue
    /// </summary>
    Task StartAsync<TMessage>(string queue, Func<TMessage, CancellationToken, Task<bool>> handler, 
        CancellationToken cancellationToken = default) where TMessage : class;

    /// <summary>
    /// Stop consuming messages
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if subscriber is healthy
    /// </summary>
    bool IsHealthy();
}

/// <summary>
/// Generic subscriber for specific message type
/// </summary>
public interface IRabbitMQSubscriber<TMessage> : IDisposable where TMessage : class
{
    /// <summary>
    /// Start consuming messages
    /// </summary>
    Task StartAsync(Func<TMessage, CancellationToken, Task<bool>> handler, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop consuming messages
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if subscriber is healthy
    /// </summary>
    bool IsHealthy();
}
