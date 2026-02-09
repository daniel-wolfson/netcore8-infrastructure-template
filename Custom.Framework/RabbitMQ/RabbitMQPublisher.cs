using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Custom.Framework.RabbitMQ;

/// <summary>
/// High-performance RabbitMQ publisher implementation
/// Optimized for 10k+ messages/second throughput
/// RabbitMQ.Client 7.2+ compatible
/// </summary>
public class RabbitMQPublisher : IRabbitMQPublisher
{
    private readonly RabbitMQOptions _options;
    private readonly ILogger<RabbitMQPublisher> _logger;
    private IConnection? _connection;
    private readonly ConcurrentBag<IChannel> _channelPool;
    private readonly SemaphoreSlim _channelSemaphore;
    private bool _disposed;

    // Private constructor - use CreateAsync factory method
    private RabbitMQPublisher(
        RabbitMQOptions options,
        ILogger<RabbitMQPublisher> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _channelPool = new ConcurrentBag<IChannel>();
        _channelSemaphore = new SemaphoreSlim(_options.ChannelsPerConnection, _options.ChannelsPerConnection);
    }

    /// <summary>
    /// Factory method to create and initialize RabbitMQPublisher asynchronously
    /// </summary>
    public static async Task<RabbitMQPublisher> CreateAsync(
        RabbitMQOptions options,
        ILogger<RabbitMQPublisher> logger,
        CancellationToken cancellationToken = default)
    {
        var publisher = new RabbitMQPublisher(options, logger);
        await publisher.InitializeAsync(cancellationToken);
        return publisher;
    }

    /// <summary>
    /// Initialize the publisher (connection, channels, infrastructure)
    /// </summary>
    private async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _connection = await CreateConnectionAsync();
            await InitializeChannelPoolAsync();
            await DeclareInfrastructureAsync();

            _logger.LogInformation("RabbitMQ Publisher initialized with {ChannelCount} channels", _options.ChannelsPerConnection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ Publisher");
            throw;
        }
    }

    public async Task PublishAsync<TMessage>(string exchange, string routingKey, TMessage message, 
        CancellationToken cancellationToken = default) where TMessage : class
    {
        var properties = CreateBasicProperties();
        await PublishAsync(exchange, routingKey, message, properties, cancellationToken);
    }

    public async Task PublishAsync<TMessage>(string exchange, string routingKey, TMessage message, 
        IBasicProperties? properties, CancellationToken cancellationToken = default) where TMessage : class
    {
        ThrowIfDisposed();

        await _channelSemaphore.WaitAsync(cancellationToken);
        IChannel? channel = null;

        try
        {
            channel = await GetChannelAsync();
            properties ??= CreateBasicProperties();

            var body = SerializeMessage(message);

            // Add metadata
            properties.MessageId = Guid.NewGuid().ToString();
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            properties.ContentType = "application/json";
            properties.Type = typeof(TMessage).Name;

            // Convert to BasicProperties for 7.2
            var basicProps = new BasicProperties
            {
                MessageId = properties.MessageId,
                Timestamp = properties.Timestamp,
                ContentType = properties.ContentType,
                Type = properties.Type,
                Persistent = _options.MessagePersistence,
                DeliveryMode = _options.MessagePersistence ? DeliveryModes.Persistent : DeliveryModes.Transient
            };

            await channel.BasicPublishAsync(
                exchange: exchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: basicProps,
                body: body,
                cancellationToken: cancellationToken);

            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug("Published message {MessageId} to {Exchange}/{RoutingKey}",
                    properties.MessageId, exchange, routingKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to {Exchange}/{RoutingKey}", exchange, routingKey);
            throw;
        }
        finally
        {
            if (channel != null)
            {
                ReturnChannel(channel);
            }
            _channelSemaphore.Release();
        }
    }

    public async Task PublishBatchAsync<TMessage>(string exchange, string routingKey, 
        IEnumerable<TMessage> messages, CancellationToken cancellationToken = default) where TMessage : class
    {
        ThrowIfDisposed();

        var messageList = messages.ToList();
        if (!messageList.Any())
        {
            return;
        }

        // For high throughput, publish messages in parallel using multiple channels
        var batchSize = Math.Max(1, messageList.Count / _options.ChannelsPerConnection);
        var batches = messageList.Chunk(batchSize);

        var tasks = batches.Select(batch => PublishBatchChunkAsync(exchange, routingKey, batch, cancellationToken));
        await Task.WhenAll(tasks);

        _logger.LogInformation("Published batch of {Count} messages to {Exchange}/{RoutingKey}",
            messageList.Count, exchange, routingKey);
    }

    private async Task PublishBatchChunkAsync<TMessage>(string exchange, string routingKey,
        IEnumerable<TMessage> messages, CancellationToken cancellationToken) where TMessage : class
    {
        foreach (var message in messages)
        {
            await PublishAsync(exchange, routingKey, message, cancellationToken);
        }
    }

    public async Task PublishToDeadLetterAsync<TMessage>(string originalExchange, string originalRoutingKey, 
        TMessage message, Exception? exception = null, int attemptCount = 1, 
        CancellationToken cancellationToken = default) where TMessage : class
    {
        var properties = CreateBasicProperties();
        properties.Headers = new Dictionary<string, object?>
        {
            ["x-original-exchange"] = originalExchange,
            ["x-original-routing-key"] = originalRoutingKey,
            ["x-death-reason"] = exception?.Message ?? "Unknown",
            ["x-death-time"] = DateTime.UtcNow.ToString("O"),
            ["x-attempt-count"] = attemptCount
        };

        await PublishAsync(_options.DeadLetterExchange, "dead", message, properties, cancellationToken);
        _logger.LogWarning("Message sent to dead letter exchange after {Attempts} attempts", attemptCount);
    }

    public Task FlushAsync(TimeSpan timeout)
    {
        // In RabbitMQ 7.2, there's no explicit flush for non-transactional publishers
        // This is here for API compatibility
        return Task.CompletedTask;
    }

    public bool IsHealthy()
    {
        return _connection?.IsOpen == true && !_disposed;
    }

    private async Task<IConnection> CreateConnectionAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            RequestedHeartbeat = TimeSpan.FromSeconds(_options.Heartbeat),
            AutomaticRecoveryEnabled = _options.AutomaticRecoveryEnabled,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(_options.NetworkRecoveryInterval),
            ClientProvidedName = $"{_options.ApplicationName}.Publisher"
        };

        return await factory.CreateConnectionAsync();
    }

    private async Task InitializeChannelPoolAsync()
    {
        if (_connection == null)
            throw new InvalidOperationException("Connection not initialized");

        for (int i = 0; i < _options.ChannelsPerConnection; i++)
        {
            var channel = await _connection.CreateChannelAsync();
            _channelPool.Add(channel);
        }
    }

    private async Task DeclareInfrastructureAsync()
    {
        if (_connection == null)
            throw new InvalidOperationException("Connection not initialized");

        var channel = await _connection.CreateChannelAsync();

        try
        {
            // Declare dead letter exchange
            await channel.ExchangeDeclareAsync(
                exchange: _options.DeadLetterExchange,
                type: "topic",
                durable: true,
                autoDelete: false);

            // Declare configured exchanges
            foreach (var (exchangeName, config) in _options.Exchanges)
            {
                await channel.ExchangeDeclareAsync(
                    exchange: exchangeName,
                    type: config.Type,
                    durable: config.Durable,
                    autoDelete: config.AutoDelete,
                    arguments: config.Arguments);

                _logger.LogInformation("Declared exchange: {Exchange} (type: {Type})", exchangeName, config.Type);
            }

            // Declare configured queues
            foreach (var (queueName, config) in _options.Queues)
            {
                var args = new Dictionary<string, object?>(config.Arguments);

                if (config.MaxLength.HasValue)
                    args["x-max-length"] = config.MaxLength.Value;

                if (config.MaxLengthBytes.HasValue)
                    args["x-max-length-bytes"] = config.MaxLengthBytes.Value;

                if (config.MessageTtl.HasValue)
                    args["x-message-ttl"] = config.MessageTtl.Value;

                args["x-dead-letter-exchange"] = _options.DeadLetterExchange;

                await channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: config.Durable,
                    exclusive: config.Exclusive,
                    autoDelete: config.AutoDelete,
                    arguments: args);

                _logger.LogInformation("Declared queue: {Queue}", queueName);

                // Bind queue to exchanges
                // If there's a binding configuration, use it; otherwise bind to matching exchange
                if (_options.Exchanges.Any())
                {
                    foreach (var (exchangeName, exchangeConfig) in _options.Exchanges)
                    {
                        // For topic exchanges, bind with routing key pattern
                        // For fanout exchanges, routing key is ignored
                        var routingKey = exchangeConfig.Type switch
                        {
                            "fanout" => "",
                            "topic" => "#", // Match all
                            "direct" => queueName,
                            _ => "#"
                        };

                        await channel.QueueBindAsync(
                            queue: queueName,
                            exchange: exchangeName,
                            routingKey: routingKey);

                        _logger.LogInformation("Bound queue {Queue} to exchange {Exchange} with routing key {RoutingKey}",
                            queueName, exchangeName, routingKey);
                    }
                }
            }
        }
        finally
        {
            await channel.CloseAsync();
        }
    }

    private async Task<IChannel> GetChannelAsync()
    {
        if (_channelPool.TryTake(out var channel) && channel.IsOpen)
        {
            return channel;
        }

        if (_connection == null)
            throw new InvalidOperationException("Connection not initialized");

        return await _connection.CreateChannelAsync();
    }

    private void ReturnChannel(IChannel channel)
    {
        if (channel.IsOpen)
        {
            _channelPool.Add(channel);
        }
        else
        {
            channel.Dispose();
        }
    }

    private IBasicProperties CreateBasicProperties()
    {
        var properties = new BasicProperties();

        if (_options.MessagePersistence)
        {
            properties.Persistent = true;
            properties.DeliveryMode = DeliveryModes.Persistent;
        }

        return properties;
    }

    private ReadOnlyMemory<byte> SerializeMessage<TMessage>(TMessage message)
    {
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(json));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RabbitMQPublisher));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        while (_channelPool.TryTake(out var channel))
        {
            try
            {
                if (channel.IsOpen)
                {
                    channel.CloseAsync().GetAwaiter().GetResult();
                }
                channel.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        try
        {
            if (_connection != null && _connection.IsOpen)
            {
                _connection.CloseAsync().GetAwaiter().GetResult();
            }
            _connection?.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }

        _channelSemaphore?.Dispose();

        _logger.LogInformation("RabbitMQ Publisher disposed");
    }
}

/// <summary>
/// Generic publisher for specific message type
/// </summary>
public class RabbitMQPublisher<TMessage> : IRabbitMQPublisher<TMessage> where TMessage : class
{
    private readonly IRabbitMQPublisher _publisher;
    private readonly string _exchange;
    private readonly string _routingKey;

    public RabbitMQPublisher(
        IRabbitMQPublisher publisher,
        string exchange,
        string routingKey)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
        _routingKey = routingKey ?? throw new ArgumentNullException(nameof(routingKey));
    }

    public Task PublishAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        return _publisher.PublishAsync(_exchange, _routingKey, message, cancellationToken);
    }

    public Task PublishBatchAsync(IEnumerable<TMessage> messages, CancellationToken cancellationToken = default)
    {
        return _publisher.PublishBatchAsync(_exchange, _routingKey, messages, cancellationToken);
    }

    public Task PublishToDeadLetterAsync(TMessage message, Exception? exception = null, 
        int attemptCount = 1, CancellationToken cancellationToken = default)
    {
        return _publisher.PublishToDeadLetterAsync(_exchange, _routingKey, message, exception, attemptCount, cancellationToken);
    }

    public void Dispose()
    {
        // Publisher is shared, don't dispose here
    }
}
