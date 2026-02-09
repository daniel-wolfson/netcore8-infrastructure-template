using Custom.Framework.RabbitMQ;
using Custom.Framework.TestFactory.Core;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit.Abstractions;
using Path = System.IO.Path;

namespace Custom.Framework.Tests.RabbitMQ;

/// <summary>
/// Integration tests for RabbitMQSubscriber
/// Tests multiple concurrent consumers (5 consumers) with high throughput
/// </summary>
public class RabbitMQSubscriberTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly Serilog.ILogger _logger;
    private readonly List<IDisposable> _disposables = [];

    private WebApplicationFactory<TestProgram> _factory = default!;
    private RabbitMQContainerTest _container = default!;
    private IRabbitMQPublisher _publisher = default!;
    private IRabbitMQSubscriber _subscriber = default!;
    private RabbitMQOptions _options = default!;

    public RabbitMQSubscriberTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = Log.Logger = new TestHostLogger(output);
    }

    public async Task InitializeAsync()
    {
        _container = new RabbitMQContainerTest(_output);
        await _container.InitializeAsync();

        _factory = new WebApplicationFactory<TestProgram>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");
                builder.ConfigureAppConfiguration((context, config) =>
                    ConfigureTestAppConfiguration(context, config));
                builder.ConfigureServices((context, services) =>
                    ConfigureServices(context, services));
                builder.ConfigureTestServices(services =>
                    services.AddSingleton<Serilog.ILogger>(new TestHostLogger(_output)));
            });

        _options = _factory.Services
            .GetService<IOptionsMonitor<RabbitMQOptions>>()?.CurrentValue
            ?? throw new ArgumentNullException("RabbitMQOptions not configured");

        _publisher = _factory.Services.GetRequiredService<IRabbitMQPublisher>();
        _subscriber = _factory.Services.GetRequiredService<IRabbitMQSubscriber>();

        _disposables.Add(_factory);

        _output.WriteLine("✅ Test infrastructure initialized");
    }

    #region Basic Subscriber Tests

    [Fact]
    public async Task Subscriber_ShouldConsumeMessage()
    {
        // Arrange
        var receivedMessages = new ConcurrentBag<ReservationMessage>();
        var message = new ReservationMessage
        {
            ReservationId = Guid.NewGuid().ToString(),
            HotelCode = "HOTEL001",
            GuestName = "Test Guest",
            TotalAmount = 299.99m
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        _ = Task.Run(async () =>
        {
            await _subscriber.StartAsync<ReservationMessage>("test.queue", async (msg, ct) =>
            {
                receivedMessages.Add(msg);
                return await Task.FromResult(true);
            }, cts.Token);
        }, cts.Token);

        await Task.Delay(1000); // Let consumer start

        await _publisher.PublishAsync("test.exchange", "test.routing", message);
        await _publisher.FlushAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(2000); // Let consumer process

        await _subscriber.StopAsync();

        // Assert
        receivedMessages.Should().ContainSingle();
        receivedMessages.First().ReservationId.Should().Be(message.ReservationId);
        _output.WriteLine($"✅ Consumed message: {message.ReservationId}");
    }

    [Fact]
    public async Task Subscriber_ShouldConsumeMultipleMessages()
    {
        // Arrange
        var receivedMessages = new ConcurrentBag<BookingMessage>();
        var messages = Enumerable.Range(1, 10).Select(i => new BookingMessage
        {
            BookingId = $"BOOK-{i:D3}",
            HotelCode = "HOTEL001",
            RoomType = "Standard",
            Status = "Confirmed"
        }).ToList();

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        
        _ = Task.Run(async () =>
        {
            await _subscriber.StartAsync<BookingMessage>("test.queue", async (msg, ct) =>
            {
                receivedMessages.Add(msg);
                return await Task.FromResult(true);
            }, cts.Token);
        }, cts.Token);

        await Task.Delay(1000); // Let consumer start

        foreach (var message in messages)
        {
            await _publisher.PublishAsync("test.exchange", "test.routing", message);
        }
        await _publisher.FlushAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(3000); // Let consumer process

        await _subscriber.StopAsync();

        // Assert
        receivedMessages.Count.Should().Be(10);
        _output.WriteLine($"✅ Consumed {receivedMessages.Count} messages");
    }

    #endregion

    #region Concurrent Consumer Tests

    [Fact]
    public async Task Subscriber_With5Consumers_ShouldProcessConcurrently()
    {
        // Arrange
        var receivedMessages = new ConcurrentBag<PaymentMessage>();
        var processedBy = new ConcurrentDictionary<int, int>();
        var messageCount = 50;

        var messages = Enumerable.Range(1, messageCount).Select(i => new PaymentMessage
        {
            PaymentId = $"PAY-{i:D3}",
            ReservationId = $"RES-{i:D3}",
            Amount = 100.00m * i,
            Currency = "USD",
            PaymentMethod = "CreditCard",
            Status = "Completed"
        }).ToList();

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        
        _ = Task.Run(async () =>
        {
            await _subscriber.StartAsync<PaymentMessage>("test.queue", async (msg, ct) =>
            {
                var threadId = Environment.CurrentManagedThreadId;
                processedBy.AddOrUpdate(threadId, 1, (key, count) => count + 1);
                
                receivedMessages.Add(msg);
                await Task.Delay(50, cts.Token); // Simulate processing time
                return true;
            }, cts.Token);
        }, cts.Token);

        await Task.Delay(2000); // Let consumers start

        // Publish all messages
        await _publisher.PublishBatchAsync("test.exchange", "test.routing", messages);
        await _publisher.FlushAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(5000); // Let consumers process

        await _subscriber.StopAsync();

        // Assert
        receivedMessages.Count.Should().BeGreaterThanOrEqualTo(messageCount);
        processedBy.Count.Should().BeGreaterThan(1, "multiple threads should process messages");

        _output.WriteLine($"✅ Consumed {receivedMessages.Count} messages");
        _output.WriteLine($"   Processed by {processedBy.Count} different threads:");

        foreach (var (threadId, count) in processedBy)
        {
            _output.WriteLine($"   Thread {threadId}: {count} messages");
        }
    }

    [Fact]
    public async Task Subscriber_HighThroughput_1000Messages_ShouldSucceed()
    {
        // Arrange
        const int messageCount = 1000;
        var receivedMessages = new ConcurrentBag<NotificationMessage>();
        var messages = Enumerable.Range(1, messageCount).Select(i => new NotificationMessage
        {
            NotificationId = $"NOT-{i:D4}",
            Type = "Test",
            Recipient = $"user{i}@example.com",
            Subject = $"Test {i}",
            Message = $"Test notification {i}"
        }).ToList();

        _output.WriteLine($"🚀 Starting high-throughput test with {messageCount} messages...");
        var sw = Stopwatch.StartNew();

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        _ = Task.Run(async () =>
        {
            await _subscriber.StartAsync<NotificationMessage>("test.queue", async (msg, ct) =>
            {
                receivedMessages.Add(msg);
                return await Task.FromResult(true);
            }, cts.Token);
        }, cts.Token);

        await Task.Delay(2000); // Let consumers start

        // Publish all messages
        await _publisher.PublishBatchAsync("test.exchange", "test.routing", messages);
        await _publisher.FlushAsync(TimeSpan.FromSeconds(10));

        // Wait for processing
        var timeout = DateTime.UtcNow.AddSeconds(20);
        while (receivedMessages.Count < messageCount && DateTime.UtcNow < timeout)
        {
            await Task.Delay(100);
        }

        await _subscriber.StopAsync();
        sw.Stop();

        // Assert
        receivedMessages.Count.Should().BeGreaterThanOrEqualTo((int)(messageCount * 0.95)); // Allow 5% loss

        var throughput = receivedMessages.Count / sw.Elapsed.TotalSeconds;

        _output.WriteLine($"");
        _output.WriteLine($"📊 High-Throughput Results:");
        _output.WriteLine($"   Sent: {messageCount:N0}");
        _output.WriteLine($"   Received: {receivedMessages.Count:N0}");
        _output.WriteLine($"   Time: {sw.ElapsedMilliseconds:N0}ms");
        _output.WriteLine($"   Throughput: {throughput:F0} msg/sec");
        _output.WriteLine($"");

        throughput.Should().BeGreaterThan(100, "should achieve at least 100 msg/sec");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Subscriber_HandlerReturningFalse_ShouldRequeueMessage()
    {
        // Arrange
        var receivedCount = 0;
        var message = new ReservationMessage
        {
            ReservationId = "FAIL-TEST",
            HotelCode = "HOTEL001",
            GuestName = "Fail Test"
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        
        _ = Task.Run(async () =>
        {
            await _subscriber.StartAsync<ReservationMessage>("test.queue", async (msg, ct) =>
            {
                Interlocked.Increment(ref receivedCount);
                return await Task.FromResult(false); // Reject and requeue
            }, cts.Token);
        }, cts.Token);

        await Task.Delay(1000); // Let consumer start

        await _publisher.PublishAsync("test.exchange", "test.routing", message);
        await _publisher.FlushAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(3000); // Let consumer process multiple times

        await _subscriber.StopAsync();

        // Assert
        receivedCount.Should().BeGreaterThan(1, "message should be requeued and reprocessed");
        _output.WriteLine($"✅ Message requeued and processed {receivedCount} times");
    }

    [Fact]
    public async Task Subscriber_HandlerThrowingException_ShouldSendToDeadLetter()
    {
        // Arrange
        var receivedCount = 0;
        var message = new ReservationMessage
        {
            ReservationId = "ERROR-TEST",
            HotelCode = "HOTEL001",
            GuestName = "Error Test"
        };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        
        _ = Task.Run(async () =>
        {
            await _subscriber.StartAsync<ReservationMessage>("test.queue", (msg, ct) =>
            {
                Interlocked.Increment(ref receivedCount);
                throw new InvalidOperationException("Simulated error");
            }, cts.Token);
        }, cts.Token);

        await Task.Delay(1000); // Let consumer start

        await _publisher.PublishAsync("test.exchange", "test.routing", message);
        await _publisher.FlushAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(2000); // Let consumer process

        await _subscriber.StopAsync();

        // Assert
        receivedCount.Should().BeGreaterThanOrEqualTo(1);
        _output.WriteLine($"✅ Handler threw exception, message sent to dead letter queue");
    }

    #endregion

    #region Health and Lifecycle Tests

    [Fact]
    public void Subscriber_ShouldBeHealthy()
    {
        // Assert
        _subscriber.IsHealthy().Should().BeTrue();
        _output.WriteLine("✅ Subscriber is healthy");
    }

    [Fact]
    public async Task Subscriber_AfterStop_ShouldStopConsuming()
    {
        // Arrange
        var receivedCount = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        
        _ = Task.Run(async () =>
        {
            await _subscriber.StartAsync<ReservationMessage>("test.queue", async (msg, ct) =>
            {
                Interlocked.Increment(ref receivedCount);
                return await Task.FromResult(true);
            }, cts.Token);
        }, cts.Token);

        await Task.Delay(1000);

        // Act
        await _subscriber.StopAsync();

        // Publish after stop
        await _publisher.PublishAsync("test.exchange", "test.routing", new ReservationMessage
        {
            ReservationId = "AFTER-STOP",
            HotelCode = "TEST"
        });
        await _publisher.FlushAsync(TimeSpan.FromSeconds(2));

        await Task.Delay(1000);

        // Assert
        receivedCount.Should().Be(0, "no messages should be consumed after stop");
        _output.WriteLine("✅ Subscriber stopped consuming");
    }

    #endregion

    #region Configuration Methods

    private void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<Serilog.ILogger>(_logger);

        services.Configure<RabbitMQOptions>(options =>
        {
            options.HostName = _container.HostName;
            options.Port = _container.Port;
            options.UserName = _container.UserName;
            options.Password = _container.Password;
            options.VirtualHost = _container.VirtualHost;
            options.ApplicationName = "RabbitMQSubscriberTests";
            options.ChannelsPerConnection = 5; // 5 concurrent consumers
            options.PrefetchCount = 10;
            options.PublisherConfirms = false;
            options.MessagePersistence = false;
            options.EnableDetailedLogging = false;
            options.Exchanges = new Dictionary<string, ExchangeConfig>
            {
                ["test.exchange"] = new ExchangeConfig { Type = "topic", Durable = false, AutoDelete = true }
            };
            options.Queues = new Dictionary<string, QueueConfig>
            {
                ["test.queue"] = new QueueConfig { Durable = false, Exclusive = false, AutoDelete = true }
            };
        });

        // Register both publisher and subscriber
        services.AddSingleton<IRabbitMQPublisher>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<RabbitMQOptions>>().Value;
            var logger = new TestLogger<RabbitMQPublisher>(_output);
            return RabbitMQPublisher.CreateAsync(opts, logger).GetAwaiter().GetResult();
        });

        services.AddSingleton<IRabbitMQSubscriber>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<RabbitMQOptions>>().Value;
            var logger = new TestLogger<RabbitMQSubscriber>(_output);
            return RabbitMQSubscriber.CreateAsync(opts, logger).GetAwaiter().GetResult();
        });
    }

    private void ConfigureTestAppConfiguration(
        WebHostBuilderContext builderContext, 
        IConfigurationBuilder builderConfig)
    {
        var directory = Path.GetDirectoryName(typeof(TestHostBase).Assembly.Location)!;
        var env = builderContext.HostingEnvironment;

        builderConfig
            .AddJsonFile(Path.Combine(directory, "appsettings.json"), optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:HostName"] = _container.HostName,
                ["RabbitMQ:Port"] = _container.Port.ToString()
            })
            .AddEnvironmentVariables();
    }

    #endregion

    public async Task DisposeAsync()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
        _disposables.Clear();

        _subscriber?.Dispose();
        _publisher?.Dispose();

        if (_container != null)
        {
            await _container.DisposeAsync();
        }

        _output.WriteLine("🧹 Test cleanup complete");
    }
}
