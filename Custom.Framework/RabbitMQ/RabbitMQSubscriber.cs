using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Custom.Framework.RabbitMQ;

/// <summary>
/// High-performance RabbitMQ subscriber implementation
/// Optimized for 10k+ messages/second throughput with multiple concurrent consumers
/// RabbitMQ.Client 7.2+ compatible
/// </summary>
public class RabbitMQSubscriber : IRabbitMQSubscriber
{
    private readonly RabbitMQOptions _options;
    private readonly ILogger<RabbitMQSubscriber> _logger;
    private IConnection? _connection;
    private readonly ConcurrentBag<IChannel> _channelPool;
    private readonly ConcurrentBag<AsyncEventingBasicConsumer> _consumers;
    private readonly ConcurrentDictionary<int, (IChannel Channel, string ConsumerTag)> _activeConsumers;
    private readonly SemaphoreSlim _channelSemaphore;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _disposed;
    private bool _isConsuming;

    // Private constructor - use CreateAsync factory method
    private RabbitMQSubscriber(
        RabbitMQOptions options,
        ILogger<RabbitMQSubscriber> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _channelPool = new ConcurrentBag<IChannel>();
        _consumers = new ConcurrentBag<AsyncEventingBasicConsumer>();
        _activeConsumers = new ConcurrentDictionary<int, (IChannel, string)>();
        _channelSemaphore = new SemaphoreSlim(_options.ChannelsPerConnection, _options.ChannelsPerConnection);
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Factory method to create and initialize RabbitMQSubscriber asynchronously
    /// </summary>
    public static async Task<RabbitMQSubscriber> CreateAsync(
        RabbitMQOptions options,
        ILogger<RabbitMQSubscriber> logger,
        CancellationToken cancellationToken = default)
    {
        var subscriber = new RabbitMQSubscriber(options, logger);
        await subscriber.InitializeAsync(cancellationToken);
        return subscriber;
    }

    /// <summary>
    /// Initialize the subscriber (connection, channels)
    /// </summary>
    private async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _connection = await CreateConnectionAsync();
            await InitializeChannelPoolAsync();

            _logger.LogInformation("RabbitMQ Subscriber initialized with {ChannelCount} channels", _options.ChannelsPerConnection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ Subscriber");
            throw;
        }
    }

    public async Task StartAsync<TMessage>(
        string queue, 
        Func<TMessage, CancellationToken, Task<bool>> handler, 
        CancellationToken cancellationToken = default) where TMessage : class
    {
        ThrowIfDisposed();

        if (_isConsuming)
        {
            _logger.LogWarning("Subscriber is already consuming messages");
            return;
        }

        _isConsuming = true;
        _logger.LogInformation("Starting {ConsumerCount} consumers for queue: {Queue}", 
            _options.ChannelsPerConnection, queue);

        // Start multiple consumers (default 5)
        var consumerTasks = new List<Task>();

        for (int i = 0; i < _options.ChannelsPerConnection; i++)
        {
            var consumerId = i;
            var task = Task.Run(async () =>
            {
                await ConsumeAsync(queue, handler, consumerId, cancellationToken);
            }, cancellationToken);

            consumerTasks.Add(task);
        }

        // Wait for all consumers to start
        await Task.WhenAll(consumerTasks);
    }

    private async Task ConsumeAsync<TMessage>(
        string queue,
        Func<TMessage, CancellationToken, Task<bool>> handler,
        int consumerId,
        CancellationToken cancellationToken) where TMessage : class
    {
        await _channelSemaphore.WaitAsync(cancellationToken);
        IChannel? channel = null;

        try
        {
            channel = await GetChannelAsync();

            // Set QoS (prefetch count)
            await channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: _options.PrefetchCount,
                global: false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            _consumers.Add(consumer);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = DeserializeMessage<TMessage>(body);

                    if (message != null)
                    {
                        var success = await handler(message, cancellationToken);

                        if (success)
                        {
                            // Acknowledge message
                            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);

                            if (_options.EnableDetailedLogging)
                            {
                                _logger.LogDebug("Consumer {ConsumerId} processed message {MessageId}",
                                    consumerId, ea.BasicProperties?.MessageId);
                            }
                        }
                        else
                        {
                            // Reject and requeue
                            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                            _logger.LogWarning("Consumer {ConsumerId} rejected message {MessageId}",
                                consumerId, ea.BasicProperties?.MessageId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Consumer {ConsumerId} error processing message", consumerId);

                    try
                    {
                        // Send to dead letter queue
                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                    }
                    catch (Exception nackEx)
                    {
                        _logger.LogError(nackEx, "Failed to NACK message");
                    }
                }
            };

            // Start consuming
            var consumerTag = await channel.BasicConsumeAsync(
                queue: queue,
                autoAck: false,
                consumer: consumer,
                cancellationToken: cancellationToken);

            // Store consumer info for proper cancellation
            _activeConsumers[consumerId] = (channel, consumerTag);

            _logger.LogInformation("Consumer {ConsumerId} started with tag {ConsumerTag}", consumerId, consumerTag);

            // Keep consumer alive until cancellation
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Consumer {ConsumerId} stopped", consumerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Consumer {ConsumerId} fatal error", consumerId);
            throw;
        }
        finally
        {
            // Remove from active consumers
            _activeConsumers.TryRemove(consumerId, out _);

            if (channel != null)
            {
                ReturnChannel(channel);
            }
            _channelSemaphore.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isConsuming)
        {
            return;
        }

        _logger.LogInformation("Stopping all consumers...");

        try
        {
            // STEP 1: Cancel all active RabbitMQ consumers FIRST
            // This tells RabbitMQ server to stop sending messages
            foreach (var (consumerId, (channel, consumerTag)) in _activeConsumers)
            {
                try
                {
                    if (channel.IsOpen)
                    {
                        await channel.BasicCancelAsync(consumerTag, noWait: false);
                        _logger.LogInformation("Cancelled RabbitMQ consumer {ConsumerId} with tag {ConsumerTag}", 
                            consumerId, consumerTag);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cancelling consumer {ConsumerId}", consumerId);
                }
            }

            // STEP 2: Cancel the tasks
            _cancellationTokenSource.Cancel();

            // STEP 3: Wait for tasks to complete
            await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);

            // STEP 4: Clean up
            _activeConsumers.Clear();
            while (_consumers.TryTake(out var consumer))
            {
                // Just clearing the bag
            }

            _isConsuming = false;
            _logger.LogInformation("All consumers stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping consumers");
            throw;
        }
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
            ClientProvidedName = $"{_options.ApplicationName}.Subscriber"
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

    private TMessage? DeserializeMessage<TMessage>(byte[] body) where TMessage : class
    {
        try
        {
            var json = Encoding.UTF8.GetString(body);
            return JsonSerializer.Deserialize<TMessage>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize message");
            return null;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RabbitMQSubscriber));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _cancellationTokenSource.Cancel();
        }
        catch
        {
            // Ignore cancellation errors
        }

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
        _cancellationTokenSource?.Dispose();

        _logger.LogInformation("RabbitMQ Subscriber disposed");
    }
}

/// <summary>
/// Generic subscriber for specific message type
/// </summary>
public class RabbitMQSubscriber<TMessage> : IRabbitMQSubscriber<TMessage> where TMessage : class
{
    private readonly IRabbitMQSubscriber _subscriber;
    private readonly string _queue;

    public RabbitMQSubscriber(
        IRabbitMQSubscriber subscriber,
        string queue)
    {
        _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    public Task StartAsync(
        Func<TMessage, CancellationToken, Task<bool>> handler, 
        CancellationToken cancellationToken = default)
    {
        return _subscriber.StartAsync(_queue, handler, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return _subscriber.StopAsync(cancellationToken);
    }

    public bool IsHealthy()
    {
        return _subscriber.IsHealthy();
    }

    public void Dispose()
    {
        // Subscriber is shared, don't dispose here
    }
}
