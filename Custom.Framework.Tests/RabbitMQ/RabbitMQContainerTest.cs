using Custom.Framework.RabbitMQ;
using Custom.Framework.TestFactory.Core;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Xunit.Abstractions;
using Path = System.IO.Path;
using static Custom.Framework.Tests.RabbitMQ.RabbitMQPublisherTests;

namespace Custom.Framework.Tests.RabbitMQ;

/// <summary>
/// Container integration tests for RabbitMQ
/// Tests basic container lifecycle and connectivity
/// Uses WebApplicationFactory pattern like KafkaTests
/// </summary>
public class RabbitMQContainerTest(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly Serilog.ILogger _logger = Log.Logger = new TestHostLogger(output);
    private readonly List<IDisposable> _disposables = [];

    private WebApplicationFactory<TestProgram> _factory = default!;
    private IContainer? _container;
    private IRabbitMQPublisher? _publisher;
    private RabbitMQOptions _settings = default!;

    public string HostName { get; private set; } = "localhost";
    public int Port { get; private set; } = 5672;
    public int ManagementPort { get; private set; } = 15672;
    public string UserName { get; private set; } = "admin";
    public string Password { get; private set; } = "123456";
    public string VirtualHost { get; private set; } = "/";

    public string ConnectionString => $"amqp://{UserName}:{Password}@{HostName}:{Port}/{VirtualHost}";

    public async Task InitializeAsync()
    {
        output.WriteLine("🐰 Initializing RabbitMQ Container Test with WebApplicationFactory...");

        try
        {
            // Find available ports
            Port = GetAvailablePort(5672);
            ManagementPort = GetAvailablePort(15672);

            output.WriteLine($"   Using AMQP port: {Port}");
            output.WriteLine($"   Using Management port: {ManagementPort}");

            // Create and start container
            _container = new ContainerBuilder()
                .WithImage("rabbitmq:3.13-management-alpine")
                .WithName($"rabbitmq-container-test-{Guid.NewGuid():N}")
                .WithPortBinding(Port, 5672)
                .WithPortBinding(ManagementPort, 15672)
                .WithEnvironment("RABBITMQ_DEFAULT_USER", UserName)
                .WithEnvironment("RABBITMQ_DEFAULT_PASS", Password)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilMessageIsLogged("Server startup complete"))
                .WithCleanUp(true)
                .Build();

            await _container.StartAsync();

            // Wait for RabbitMQ to be fully ready
            await Task.Delay(TimeSpan.FromSeconds(5));

            output.WriteLine("✅ RabbitMQ Container started successfully");
            output.WriteLine($"   Connection: {ConnectionString}");
            output.WriteLine($"   Management UI: http://{HostName}:{ManagementPort}");

            // Initialize WebApplicationFactory (like KafkaTests)
            _factory = new WebApplicationFactory<TestProgram>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Test");
                    builder.ConfigureAppConfiguration((context, config) =>
                        ConfigureTestAppConfiguration(context, config));
                    builder.ConfigureServices((context, services) =>
                        ConfigureServices(context, services));
                    builder.ConfigureTestServices(services =>
                        services.AddSingleton<Serilog.ILogger>(new TestHostLogger(output)));
                });

            // Get settings from DI
            _settings = _factory.Services
                .GetService<IOptionsMonitor<RabbitMQOptions>>()?.CurrentValue
                ?? throw new ArgumentNullException("RabbitMQOptions not configured");

            // Initialize publisher from factory
            _publisher = _factory.Services.GetService<IRabbitMQPublisher>()
                ?? throw new ArgumentNullException("IRabbitMQPublisher not registered");

            _disposables.Add(_factory);

            output.WriteLine("✅ RabbitMQ Container Test initialized");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Failed to initialize RabbitMQ container: {ex.Message}");
            throw;
        }
    }

    #region Container Tests

    [Fact]
    public void Container_ShouldBeRunning()
    {
        // Assert
        _container.Should().NotBeNull();
        _container!.State.Should().Be(TestcontainersStates.Running);
        output.WriteLine("✅ Container is running");
    }

    [Fact]
    public void Container_ShouldHaveCorrectPorts()
    {
        // Assert
        Port.Should().BeGreaterThanOrEqualTo(5672);
        ManagementPort.Should().BeGreaterThanOrEqualTo(15672);
        output.WriteLine($"✅ Ports configured: AMQP={Port}, Management={ManagementPort}");
    }

    [Fact]
    public void ConnectionString_ShouldBeValid()
    {
        // Assert
        ConnectionString.Should().StartWith("amqp://");
        ConnectionString.Should().Contain("localhost");
        ConnectionString.Should().Contain(Port.ToString());
        output.WriteLine($"✅ Connection string valid: {ConnectionString}");
    }

    [Fact]
    public void Settings_ShouldBeLoadedFromConfiguration()
    {
        // Assert
        _settings.Should().NotBeNull();
        _settings.HostName.Should().Be(HostName);
        _settings.Port.Should().Be(Port);
        output.WriteLine($"✅ Settings loaded from configuration");
    }

    #endregion

    #region Publisher Tests

    [Fact]
    public void Publisher_ShouldBeInitializedFromDI()
    {
        // Assert
        _publisher.Should().NotBeNull();
        _publisher!.IsHealthy().Should().BeTrue();
        output.WriteLine("✅ Publisher initialized from DI and healthy");
    }

    [Fact]
    public async Task Publisher_ShouldPublishMessage()
    {
        // Arrange
        var message = new ReservationMessage
        {
            ReservationId = Guid.NewGuid().ToString(),
            HotelCode = "HOTEL-CONTAINER-TEST",
            GuestName = "Container Test Guest",
            CheckInDate = DateTime.Today.AddDays(1),
            CheckOutDate = DateTime.Today.AddDays(3),
            RoomNumber = 101,
            TotalAmount = 299.99m
        };

        // Act
        await _publisher!.PublishAsync("test.container.exchange", "test.routing", message);

        // Assert
        _publisher.IsHealthy().Should().BeTrue();
        output.WriteLine($"✅ Published test message: {message.ReservationId}");
    }

    [Fact]
    public async Task Publisher_ShouldPublishMultipleMessages()
    {
        // Arrange
        var messages = Enumerable.Range(1, 10).Select(i => new BookingMessage
        {
            BookingId = $"BOOK-{i:D3}",
            HotelCode = "HOTEL-TEST",
            RoomType = i % 2 == 0 ? "Standard" : "Deluxe",
            Status = "Confirmed"
        }).ToList();

        // Act
        foreach (var message in messages)
        {
            await _publisher!.PublishAsync("test.container.exchange", "test.batch", message);
        }

        // Assert
        _publisher!.IsHealthy().Should().BeTrue();
        output.WriteLine($"✅ Published {messages.Count} messages successfully");
    }

    [Fact]
    public async Task Publisher_ShouldPublishBatch()
    {
        // Arrange
        var messages = Enumerable.Range(1, 50).Select(i => new PaymentMessage
        {
            PaymentId = $"PAY-{i:D3}",
            ReservationId = $"RES-{i:D3}",
            Amount = 100.00m * i,
            Currency = "USD",
            PaymentMethod = "CreditCard",
            Status = "Completed"
        }).ToList();

        // Act
        await _publisher!.PublishBatchAsync("test.container.exchange", "test.batch", messages);

        // Assert
        _publisher.IsHealthy().Should().BeTrue();
        output.WriteLine($"✅ Published batch of {messages.Count} messages");
    }

    #endregion

    #region Lifecycle Tests

    [Fact]
    public async Task Container_ShouldRestart()
    {
        // Arrange
        _container.Should().NotBeNull();

        // Act
        await _container!.StopAsync();
        await Task.Delay(2000);
        await _container.StartAsync();
        await Task.Delay(3000);

        // Assert
        _container.State.Should().Be(DotNet.Testcontainers.Containers.TestcontainersStates.Running);
        output.WriteLine("✅ Container restarted successfully");
    }

    [Fact]
    public async Task Publisher_ShouldRecoverAfterReconnect()
    {
        // Arrange
        _publisher.Should().NotBeNull();
        var message1 = new NotificationMessage 
        { 
            NotificationId = "MSG-1", 
            Type = "Test",
            Recipient = "test@example.com",
            Subject = "Before",
            Message = "Test message before reconnect"
        };

        // Act - Publish before simulated connection issue
        await _publisher!.PublishAsync("test.container.exchange", "test.recovery", message1);

        // Simulate brief connection issue
        await Task.Delay(100);

        var message2 = new NotificationMessage 
        { 
            NotificationId = "MSG-2", 
            Type = "Test",
            Recipient = "test@example.com",
            Subject = "After",
            Message = "Test message after reconnect"
        };
        await _publisher.PublishAsync("test.container.exchange", "test.recovery", message2);

        // Assert
        _publisher.IsHealthy().Should().BeTrue();
        output.WriteLine("✅ Publisher recovered and published after reconnect");
    }

    #endregion

    #region Configuration Methods (like KafkaTests)

    private void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        var configuration = context.Configuration;

        // Replace the logger with test-specific one
        var existingLogger = services.FirstOrDefault(d => d.ServiceType == typeof(Serilog.ILogger));
        if (existingLogger != null)
            services.Remove(existingLogger);

        services.AddSingleton<Serilog.ILogger>(_logger);

        // Configure RabbitMQ options
        services.Configure<RabbitMQOptions>(options =>
        {
            options.HostName = HostName;
            options.Port = Port;
            options.UserName = UserName;
            options.Password = Password;
            options.VirtualHost = VirtualHost;
            options.ApplicationName = "RabbitMQContainerTest";
            options.ChannelsPerConnection = 5;
            options.PublisherConfirms = false;
            options.MessagePersistence = false;
            options.EnableDetailedLogging = true;
            options.Exchanges = new Dictionary<string, ExchangeConfig>
            {
                ["test.container.exchange"] = new ExchangeConfig
                {
                    Type = "topic",
                    Durable = false,
                    AutoDelete = true
                }
            };
            options.Queues = new Dictionary<string, QueueConfig>
            {
                ["test.container.queue"] = new QueueConfig
                {
                    Durable = false,
                    Exclusive = false,
                    AutoDelete = true
                }
            };
        });

        // Register RabbitMQ publisher
        services.AddSingleton<IRabbitMQPublisher>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMQOptions>>().Value;
            var logger = new TestLogger<RabbitMQPublisher>(output);
            return RabbitMQPublisher.CreateAsync(options, logger).GetAwaiter().GetResult();
        });

        output.WriteLine("✅ Services configured");
    }

    private void ConfigureTestAppConfiguration(
        WebHostBuilderContext builderContext, 
        IConfigurationBuilder builderConfig)
    {
        var directory = Path.GetDirectoryName(typeof(TestHostBase).Assembly.Location)!;
        var env = builderContext.HostingEnvironment;
        var environmentName = env.EnvironmentName;

        builderConfig
            .AddJsonFile(Path.Combine(directory, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(directory, $"appsettings.{environmentName}.json"), optional: true, reloadOnChange: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:HostName"] = HostName,
                ["RabbitMQ:Port"] = Port.ToString(),
                ["RabbitMQ:UserName"] = UserName,
                ["RabbitMQ:Password"] = Password
            })
            .AddEnvironmentVariables();

        output.WriteLine("✅ Configuration loaded");
    }

    #endregion

    public async Task DisposeAsync()
    {
        try
        {
            output.WriteLine("🧹 Cleaning up RabbitMQ Container Test...");

            // Dispose all registered disposables
            foreach (var disposable in _disposables)
            {
                try
                {
                    disposable?.Dispose();
                }
                catch (Exception ex)
                {
                    output.WriteLine($"⚠️  Error disposing resource: {ex.Message}");
                }
            }
            _disposables.Clear();

            // Dispose publisher explicitly
            if (_publisher != null)
            {
                _publisher.Dispose();
                output.WriteLine("   ✅ Publisher disposed");
            }

            // Stop and dispose container
            if (_container != null)
            {
                await _container.StopAsync();
                await _container.DisposeAsync();
                output.WriteLine("   ✅ Container stopped");
            }

            output.WriteLine("✅ RabbitMQ Container Test cleanup complete");
        }
        catch (Exception ex)
        {
            output.WriteLine($"⚠️  Cleanup warning: {ex.Message}");
        }
    }

    private static int GetAvailablePort(int startingPort)
    {
        var port = startingPort;
        while (port < startingPort + 100)
        {
            try
            {
                var listener = new System.Net.Sockets.TcpListener(
                    System.Net.IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch
            {
                port++;
            }
        }
        throw new InvalidOperationException($"Could not find available port starting from {startingPort}");
    }
}
