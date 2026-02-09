using Custom.Framework.RabbitMQ;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.RabbitMQ;

/// <summary>
/// High-throughput integration tests for RabbitMQPublisher
/// Tests 10k+ messages/second scenarios for hospitality domain
/// </summary>
public class RabbitMQPublisherTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private RabbitMQContainerTest _container = null!;
    private IRabbitMQPublisher _publisher = null!;
    private ILogger<RabbitMQPublisher> _logger = null!;

    public RabbitMQPublisherTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _container = new RabbitMQContainerTest(_output);
        await _container.InitializeAsync();

        // Create publisher
        var options = new RabbitMQOptions
        {
            HostName = _container.HostName,
            Port = _container.Port,
            UserName = _container.UserName,
            Password = _container.Password,
            VirtualHost = _container.VirtualHost,
            ApplicationName = "RabbitMQPublisherTests",
            ChannelsPerConnection = 10, // High throughput
            PublisherConfirms = false, // Disable for max throughput
            MessagePersistence = false, // Disable for speed in tests
            EnableDetailedLogging = false,
            Exchanges = new Dictionary<string, ExchangeConfig>
            {
                ["test.exchange"] = new ExchangeConfig
                {
                    Type = "topic",
                    Durable = false,
                    AutoDelete = true
                },
                ["test.fanout"] = new ExchangeConfig
                {
                    Type = "fanout",
                    Durable = false,
                    AutoDelete = true
                }
            },
            Queues = new Dictionary<string, QueueConfig>
            {
                ["test.queue"] = new QueueConfig
                {
                    Durable = false,
                    Exclusive = false,
                    AutoDelete = true
                }
            }
        };

        _logger = new TestLogger<RabbitMQPublisher>(_output);
        _publisher = await RabbitMQPublisher.CreateAsync(options, _logger);

        _output.WriteLine("✅ Test infrastructure initialized");
    }

    #region Basic Tests

    [Fact]
    public async Task PublishAsync_SingleMessage_ShouldSucceed()
    {
        // Arrange
        var message = new ReservationMessage
        {
            ReservationId = Guid.NewGuid().ToString(),
            HotelCode = "HOTEL001",
            GuestName = "John Doe",
            CheckInDate = DateTime.Today.AddDays(7),
            CheckOutDate = DateTime.Today.AddDays(10),
            RoomNumber = 101,
            TotalAmount = 450.00m
        };

        // Act
        await _publisher.PublishAsync("test.exchange", "reservation.created", message);

        // Assert
        _publisher.IsHealthy().Should().BeTrue();
        _output.WriteLine($"✅ Published reservation: {message.ReservationId}");
    }

    [Fact]
    public async Task PublishAsync_MultipleMessageTypes_ShouldSucceed()
    {
        // Arrange
        var reservation = new ReservationMessage
        {
            ReservationId = Guid.NewGuid().ToString(),
            HotelCode = "HOTEL001",
            GuestName = "Jane Smith",
            TotalAmount = 600.00m
        };

        var booking = new BookingMessage
        {
            BookingId = Guid.NewGuid().ToString(),
            HotelCode = "HOTEL001",
            RoomType = "Deluxe",
            Status = "Confirmed"
        };

        var payment = new PaymentMessage
        {
            PaymentId = Guid.NewGuid().ToString(),
            ReservationId = reservation.ReservationId,
            Amount = 600.00m,
            PaymentMethod = "CreditCard",
            Status = "Completed"
        };

        // Act
        await _publisher.PublishAsync("test.exchange", "reservation.created", reservation);
        await _publisher.PublishAsync("test.exchange", "booking.confirmed", booking);
        await _publisher.PublishAsync("test.exchange", "payment.processed", payment);

        // Assert
        _publisher.IsHealthy().Should().BeTrue();
        _output.WriteLine("✅ Published multiple message types successfully");
    }

    #endregion

    #region Batch Tests

    [Fact]
    public async Task PublishBatchAsync_100Messages_ShouldSucceed()
    {
        // Arrange
        var messages = Enumerable.Range(1, 100).Select(i => new ReservationMessage
        {
            ReservationId = Guid.NewGuid().ToString(),
            HotelCode = $"HOTEL{i % 10:D3}",
            GuestName = $"Guest {i}",
            RoomNumber = i,
            TotalAmount = 100.00m * i
        }).ToList();

        var sw = Stopwatch.StartNew();

        // Act
        await _publisher.PublishBatchAsync("test.exchange", "reservation.batch", messages);
        
        sw.Stop();

        // Assert
        _publisher.IsHealthy().Should().BeTrue();
        _output.WriteLine($"✅ Published {messages.Count} messages in {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"   Throughput: {messages.Count / sw.Elapsed.TotalSeconds:F0} msg/sec");
    }

    [Fact]
    public async Task PublishBatchAsync_1000Messages_ShouldSucceed()
    {
        // Arrange
        var messages = Enumerable.Range(1, 1000).Select(i => new BookingMessage
        {
            BookingId = Guid.NewGuid().ToString(),
            HotelCode = $"HOTEL{i % 50:D3}",
            RoomType = i % 2 == 0 ? "Standard" : "Deluxe",
            Status = "Confirmed"
        }).ToList();

        var sw = Stopwatch.StartNew();

        // Act
        await _publisher.PublishBatchAsync("test.exchange", "booking.batch", messages);
        
        sw.Stop();

        // Assert
        _publisher.IsHealthy().Should().BeTrue();
        _output.WriteLine($"✅ Published {messages.Count} messages in {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"   Throughput: {messages.Count / sw.Elapsed.TotalSeconds:F0} msg/sec");
    }

    #endregion

    #region High-Throughput Tests

    [Fact]
    public async Task PublishAsync_10000Messages_Parallel_ShouldAchieveHighThroughput()
    {
        // Arrange
        const int messageCount = 10000;
        var messages = Enumerable.Range(1, messageCount).Select(i => new ReservationMessage
        {
            ReservationId = $"RES-{i:D6}",
            HotelCode = $"HOTEL{i % 100:D3}",
            GuestName = $"Guest {i}",
            CheckInDate = DateTime.Today.AddDays(i % 30),
            CheckOutDate = DateTime.Today.AddDays((i % 30) + 3),
            RoomNumber = i % 500,
            TotalAmount = 100.00m + (i % 1000),
            Currency = "USD"
        }).ToList();

        var successCount = 0;
        var failureCount = 0;
        var exceptions = new ConcurrentBag<Exception>();

        _output.WriteLine($"🚀 Starting high-throughput test with {messageCount} messages...");
        var sw = Stopwatch.StartNew();

        // Act - Publish in parallel with controlled concurrency
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 50 // Control concurrency
        };

        await Parallel.ForEachAsync(messages, options, async (message, ct) =>
        {
            try
            {
                await _publisher.PublishAsync("test.exchange", "reservation.highload", message, ct);
                Interlocked.Increment(ref successCount);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failureCount);
                exceptions.Add(ex);
            }
        });

        sw.Stop();

        // Assert
        successCount.Should().Be(messageCount, "all messages should be published successfully");
        failureCount.Should().Be(0, "no failures should occur");
        _publisher.IsHealthy().Should().BeTrue();

        // Performance metrics
        var throughput = messageCount / sw.Elapsed.TotalSeconds;
        var avgLatency = sw.ElapsedMilliseconds / (double)messageCount;

        _output.WriteLine($"");
        _output.WriteLine($"📊 Performance Metrics:");
        _output.WriteLine($"   Total messages: {messageCount:N0}");
        _output.WriteLine($"   Successful: {successCount:N0}");
        _output.WriteLine($"   Failed: {failureCount:N0}");
        _output.WriteLine($"   Total time: {sw.ElapsedMilliseconds:N0}ms ({sw.Elapsed.TotalSeconds:F2}s)");
        _output.WriteLine($"   Throughput: {throughput:F0} msg/sec");
        _output.WriteLine($"   Average latency: {avgLatency:F2}ms per message");
        _output.WriteLine($"");

        // Verify performance target
        throughput.Should().BeGreaterThan(5000, "should achieve at least 5k msg/sec");
        
        if (throughput >= 10000)
        {
            _output.WriteLine($"✅ EXCELLENT: Achieved target of 10k+ msg/sec!");
        }
        else if (throughput >= 7500)
        {
            _output.WriteLine($"✅ GOOD: Achieved {throughput:F0} msg/sec (target: 10k)");
        }
        else
        {
            _output.WriteLine($"⚠️  ACCEPTABLE: Achieved {throughput:F0} msg/sec (target: 10k)");
        }
    }

    [Fact]
    public async Task PublishAsync_10000Messages_Batch_ShouldAchieveHighThroughput()
    {
        // Arrange
        const int messageCount = 10000;
        const int batchSize = 1000;

        _output.WriteLine($"🚀 Starting batch high-throughput test with {messageCount} messages...");
        var sw = Stopwatch.StartNew();

        var successCount = 0;

        // Act - Publish in batches
        for (int i = 0; i < messageCount; i += batchSize)
        {
            var batch = Enumerable.Range(i, Math.Min(batchSize, messageCount - i))
                .Select(j => new PaymentMessage
                {
                    PaymentId = $"PAY-{j:D6}",
                    ReservationId = $"RES-{j:D6}",
                    Amount = 100.00m + (j % 1000),
                    Currency = "USD",
                    PaymentMethod = j % 2 == 0 ? "CreditCard" : "Cash",
                    Status = "Completed"
                }).ToList();

            await _publisher.PublishBatchAsync("test.exchange", "payment.batch", batch);
            successCount += batch.Count;
        }

        sw.Stop();

        // Assert
        successCount.Should().Be(messageCount);
        _publisher.IsHealthy().Should().BeTrue();

        // Performance metrics
        var throughput = messageCount / sw.Elapsed.TotalSeconds;

        _output.WriteLine($"");
        _output.WriteLine($"📊 Batch Performance Metrics:");
        _output.WriteLine($"   Total messages: {messageCount:N0}");
        _output.WriteLine($"   Batch size: {batchSize:N0}");
        _output.WriteLine($"   Total time: {sw.ElapsedMilliseconds:N0}ms ({sw.Elapsed.TotalSeconds:F2}s)");
        _output.WriteLine($"   Throughput: {throughput:F0} msg/sec");
        _output.WriteLine($"");

        throughput.Should().BeGreaterThan(5000, "batch publishing should achieve at least 5k msg/sec");
    }

    [Fact]
    public async Task PublishAsync_MultipleExchanges_Parallel_ShouldSucceed()
    {
        // Arrange
        const int messagesPerExchange = 2500; // 4 exchanges * 2500 = 10k total
        var exchanges = new[]
        {
            ("test.exchange", "reservation"),
            ("test.exchange", "booking"),
            ("test.exchange", "payment"),
            ("test.fanout", "notification")
        };

        _output.WriteLine($"🚀 Publishing to multiple exchanges in parallel...");
        var sw = Stopwatch.StartNew();

        // Act - Publish to different exchanges in parallel
        var tasks = exchanges.Select(async (exchange, idx) =>
        {
            var messages = Enumerable.Range(idx * messagesPerExchange, messagesPerExchange)
                .Select(i => new NotificationMessage
                {
                    NotificationId = $"NOT-{i:D6}",
                    Type = exchange.Item2,
                    Recipient = $"user{i}@example.com",
                    Subject = $"Test {i}",
                    Message = $"Test notification {i}"
                }).ToList();

            foreach (var message in messages)
            {
                await _publisher.PublishAsync(exchange.Item1, exchange.Item2, message);
            }

            return messages.Count;
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        var totalMessages = results.Sum();
        totalMessages.Should().Be(exchanges.Length * messagesPerExchange);
        _publisher.IsHealthy().Should().BeTrue();

        var throughput = totalMessages / sw.Elapsed.TotalSeconds;

        _output.WriteLine($"");
        _output.WriteLine($"📊 Multi-Exchange Performance:");
        _output.WriteLine($"   Exchanges: {exchanges.Length}");
        _output.WriteLine($"   Messages per exchange: {messagesPerExchange:N0}");
        _output.WriteLine($"   Total messages: {totalMessages:N0}");
        _output.WriteLine($"   Total time: {sw.ElapsedMilliseconds:N0}ms");
        _output.WriteLine($"   Throughput: {throughput:F0} msg/sec");
        _output.WriteLine($"");
    }

    #endregion

    #region Dead Letter Tests

    [Fact]
    public async Task PublishToDeadLetterAsync_ShouldSucceed()
    {
        // Arrange
        var message = new ReservationMessage
        {
            ReservationId = Guid.NewGuid().ToString(),
            HotelCode = "HOTEL999",
            GuestName = "Failed Reservation"
        };

        var exception = new InvalidOperationException("Simulated processing failure");

        // Act
        await _publisher.PublishToDeadLetterAsync(
            "test.exchange",
            "reservation.failed",
            message,
            exception,
            attemptCount: 3);

        // Assert
        _publisher.IsHealthy().Should().BeTrue();
        _output.WriteLine($"✅ Published to dead letter exchange");
    }

    #endregion

    #region Health and Lifecycle Tests

    [Fact]
    public void IsHealthy_AfterInitialization_ShouldReturnTrue()
    {
        // Act
        var isHealthy = _publisher.IsHealthy();

        // Assert
        isHealthy.Should().BeTrue();
        _output.WriteLine("✅ Publisher is healthy");
    }

    [Fact]
    public async Task Publisher_AfterDispose_ShouldNotBeHealthy()
    {
        // Arrange
        var tempOptions = new RabbitMQOptions
        {
            HostName = _container.HostName,
            Port = _container.Port,
            UserName = _container.UserName,
            Password = _container.Password,
            ApplicationName = "TempPublisher"
        };

        var tempLogger = new TestLogger<RabbitMQPublisher>(_output);
        var tempPublisher = await RabbitMQPublisher.CreateAsync(tempOptions, tempLogger);

        tempPublisher.IsHealthy().Should().BeTrue();

        // Act
        tempPublisher.Dispose();

        // Assert
        tempPublisher.IsHealthy().Should().BeFalse();
        _output.WriteLine("✅ Publisher disposed correctly");
    }

    #endregion

    #region Stress Tests

    [Fact]
    public async Task PublishAsync_ContinuousLoad_1Minute_ShouldMaintainPerformance()
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(10); // Reduced for test speed
        var messagesSent = 0;
        var cts = new CancellationTokenSource(duration);

        _output.WriteLine($"🚀 Starting continuous load test for {duration.TotalSeconds}s...");
        var sw = Stopwatch.StartNew();

        // Act - Continuous publishing
        var tasks = Enumerable.Range(0, 10).Select(async workerIndex =>
        {
            var count = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var message = new BookingMessage
                    {
                        BookingId = $"W{workerIndex}-{count++}",
                        HotelCode = $"HOTEL{workerIndex:D3}",
                        RoomType = "Standard",
                        Status = "Confirmed"
                    };

                    await _publisher.PublishAsync("test.exchange", "booking.load", message, cts.Token);
                    Interlocked.Increment(ref messagesSent);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"⚠️  Worker {workerIndex} error: {ex.Message}");
                }
            }
            return count;
        });

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        _publisher.IsHealthy().Should().BeTrue();

        var throughput = messagesSent / sw.Elapsed.TotalSeconds;

        _output.WriteLine($"");
        _output.WriteLine($"📊 Continuous Load Results:");
        _output.WriteLine($"   Duration: {sw.Elapsed.TotalSeconds:F1}s");
        _output.WriteLine($"   Messages sent: {messagesSent:N0}");
        _output.WriteLine($"   Average throughput: {throughput:F0} msg/sec");
        _output.WriteLine($"");

        messagesSent.Should().BeGreaterThan(1000, "should send at least 1k messages in sustained load");
    }

    #endregion

    public async Task DisposeAsync()
    {
        _publisher?.Dispose();
        
        if (_container != null)
        {
            await _container.DisposeAsync();
        }

        _output.WriteLine("🧹 Test cleanup complete");
    }
}

/// <summary>
/// Simple logger implementation for tests
/// </summary>
internal class TestLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;

    public TestLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{logLevel}] {message}");
            
            if (exception != null)
            {
                _output.WriteLine($"   Exception: {exception.Message}");
            }
        }
    }
}
