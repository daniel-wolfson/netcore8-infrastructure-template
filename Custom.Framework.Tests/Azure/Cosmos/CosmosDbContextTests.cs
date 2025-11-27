using Custom.Framework.Azure.Cosmos;
using Custom.Framework.Azure.Cosmos.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Azure.Cosmos;

/// <summary>
/// Integration tests for OrderDbContext
/// Tests EF Core Cosmos provider functionality and configuration
/// </summary>
public class CosmosDbContextTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IHost _testHost = default!;
    private OrderDbContext _context = default!;
    private CosmosTestContainer _cosmosContainer = default!;

    public CosmosDbContextTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _cosmosContainer = new CosmosTestContainer(_output);
        await _cosmosContainer.InitializeAsync();

        _testHost = await new HostBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                var cosmosConfig = new Dictionary<string, string?>
                {
                    ["CosmosDB:UseEmulator"] = "true",
                    ["CosmosDB:AccountEndpoint"] = _cosmosContainer.AccountEndpoint,
                    ["CosmosDB:AccountKey"] = _cosmosContainer.AccountKey,
                    ["CosmosDB:DatabaseName"] = _cosmosContainer.DatabaseName,
                    ["CosmosDB:ContainerName"] = "OrdersDbContextTest",
                    ["CosmosDB:PartitionKeyPath"] = "/hotelCode",
                    ["CosmosDB:EnableDetailedLogging"] = "true"
                };

                config.AddInMemoryCollection(cosmosConfig);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(b =>
                {
                    b.AddConsole();
                    b.SetMinimumLevel(LogLevel.Debug);
                });
                services.AddCosmosDbForOrders(context.Configuration);
            })
            .StartAsync();

        await _testHost.UseCosmosDbAsync();

        _context = _testHost.Services.GetRequiredService<OrderDbContext>();

        _output.WriteLine("? DbContext test host initialized");
    }

    #region Connection Tests

    [Fact]
    public async Task DatabaseConnection_ShouldBeConfigured()
    {
        // Act - Use CosmosDbInitializer instead of DbContext.CanConnectAsync
        var initializer = _testHost.Services.GetRequiredService<CosmosDbInitializer>();
        var canConnect = await initializer.CanConnectAsync();

        // Assert
        canConnect.Should().BeTrue();
        _output.WriteLine("✅ Database connection verified");
    }

    [Fact]
    public void DbContext_ShouldHaveCorrectConfiguration()
    {
        // Act
        var serviceProvider = _testHost.Services;
        var options = serviceProvider.GetService<CosmosDbOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.DatabaseName.Should().Be(_cosmosContainer.DatabaseName);
        options.ContainerName.Should().Be("OrdersDbContextTest");
        options.PartitionKeyPath.Should().Be("/hotelCode");

        _output.WriteLine($"✅ DbContext configured correctly");
        _output.WriteLine($"   Database: {options.DatabaseName}");
        _output.WriteLine($"   Container: {options.ContainerName}");
    }

    #endregion

    #region DbSet Tests

    [Fact]
    public async Task DbSet_Add_ShouldAddOrder()
    {
        // Arrange
        var order = new OrderContext
        {
            HotelCode = "HOTEL001",
            SessionId = Guid.NewGuid().ToString(),
            Status = OrderStatus.Pending,
            CurrentStep = "Test"
        };

        // Act
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Assert
        order.Id.Should().NotBeNullOrEmpty();
        _output.WriteLine($"? Added order via DbSet: {order.Id}");
    }

    [Fact]
    public async Task DbSet_Find_ShouldRetrieveOrder()
    {
        // Arrange
        var order = new OrderContext
        {
            HotelCode = "HOTEL002",
            SessionId = Guid.NewGuid().ToString(),
            Status = OrderStatus.Pending
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Act
        var found = await _context.Orders.FindAsync(order.Id);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(order.Id);
        _output.WriteLine($"? Found order: {found.Id}");
    }

    [Fact]
    public async Task DbSet_Remove_ShouldDeleteOrder()
    {
        // Arrange
        var order = new OrderContext
        {
            HotelCode = "HOTEL003",
            SessionId = Guid.NewGuid().ToString(),
            Status = OrderStatus.Pending
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Act
        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();

        var found = await _context.Orders.FindAsync(order.Id);

        // Assert
        found.Should().BeNull();
        _output.WriteLine($"? Deleted order: {order.Id}");
    }

    #endregion

    #region Partition Key Tests

    [Fact]
    public async Task WithPartitionKey_ShouldQueryWithinPartition()
    {
        // Arrange
        var hotelCode = "HOTEL_PARTITION";
        var order1 = new OrderContext
        {
            HotelCode = hotelCode,
            SessionId = Guid.NewGuid().ToString(),
            Status = OrderStatus.Pending
        };
        var order2 = new OrderContext
        {
            HotelCode = hotelCode,
            SessionId = Guid.NewGuid().ToString(),
            Status = OrderStatus.Succeeded
        };

        _context.Orders.AddRange(order1, order2);
        await _context.SaveChangesAsync();

        // Act
        var orders = await _context.Orders
            .WithPartitionKey(hotelCode)
            .ToListAsync();

        // Assert
        orders.Count.Should().BeGreaterThanOrEqualTo(2);
        orders.All(o => o.HotelCode == hotelCode).Should().BeTrue();
        _output.WriteLine($"? Retrieved {orders.Count} orders for partition: {hotelCode}");
    }

    [Fact]
    public async Task WithPartitionKey_SingleQuery_ShouldBeEfficient()
    {
        // Arrange
        var hotelCode = "HOTEL_EFFICIENT";
        var sessionId = Guid.NewGuid().ToString();
        
        var order = new OrderContext
        {
            HotelCode = hotelCode,
            SessionId = sessionId,
            Status = OrderStatus.Pending
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Act - Query with partition key (efficient)
        var found = await _context.Orders
            .WithPartitionKey(hotelCode)
            .FirstOrDefaultAsync(o => o.SessionId == sessionId);

        // Assert
        found.Should().NotBeNull();
        found!.SessionId.Should().Be(sessionId);
        _output.WriteLine($"? Efficient partition-scoped query succeeded");
    }

    #endregion

    #region Complex Property Tests

    [Fact]
    public async Task OwnedTypes_ShouldPersistAndRetrieve()
    {
        // Arrange
        var order = new OrderContext
        {
            HotelCode = "HOTEL_OWNED",
            SessionId = Guid.NewGuid().ToString(),
            Status = OrderStatus.Pending,
            OrderData = new OrderData
            {
                CheckInDate = DateTime.Today.AddDays(1),
                CheckOutDate = DateTime.Today.AddDays(3),
                Adults = 2,
                Children = 1,
                RoomCode = "DELUXE",
                TotalAmount = 500.00m,
                CurrencyCode = "USD",
                GuestInfo = new GuestInfo
                {
                    FirstName = "Jane",
                    LastName = "Smith",
                    Email = "jane@example.com",
                    Phone = "+1234567890",
                    Country = "USA"
                }
            },
            PaymentInfo = new PaymentInfo
            {
                PaymentMethod = "CreditCard",
                TransactionId = "TXN123",
                Amount = 500.00m,
                Currency = "USD",
                PaymentStatus = "Pending"
            },
            VerificationResult = new VerificationResult
            {
                IsVerified = true,
                VerificationErrors = new List<string>()
            }
        };

        // Act
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var retrieved = await _context.Orders
            .WithPartitionKey(order.HotelCode)
            .FirstOrDefaultAsync(o => o.Id == order.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.OrderData.Should().NotBeNull();
        retrieved.OrderData.GuestInfo.Should().NotBeNull();
        retrieved.OrderData.GuestInfo!.FirstName.Should().Be("Jane");
        retrieved.PaymentInfo.Should().NotBeNull();
        retrieved.PaymentInfo!.TransactionId.Should().Be("TXN123");
        retrieved.VerificationResult.Should().NotBeNull();
        retrieved.VerificationResult!.IsVerified.Should().BeTrue();

        _output.WriteLine("? Owned types persisted and retrieved correctly");
        _output.WriteLine($"   Guest: {retrieved.OrderData.GuestInfo.FirstName} {retrieved.OrderData.GuestInfo.LastName}");
        _output.WriteLine($"   Payment: {retrieved.PaymentInfo.TransactionId}");
    }

    [Fact]
    public async Task Metadata_Dictionary_ShouldPersistAndRetrieve()
    {
        // Arrange
        var order = new OrderContext
        {
            HotelCode = "HOTEL_META",
            SessionId = Guid.NewGuid().ToString(),
            Status = OrderStatus.Pending,
            Metadata = new Dictionary<string, string>
            {
                ["source"] = "mobile_app",
                ["version"] = "2.1.0",
                ["campaign"] = "summer_promotion"
            }
        };

        // Act
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var retrieved = await _context.Orders
            .WithPartitionKey(order.HotelCode)
            .FirstOrDefaultAsync(o => o.Id == order.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Metadata.Should().NotBeNull();
        retrieved.Metadata.Should().ContainKey("source");
        retrieved.Metadata["source"].Should().Be("mobile_app");
        retrieved.Metadata.Should().HaveCount(3);

        _output.WriteLine("? Metadata dictionary persisted correctly");
        _output.WriteLine($"   Source: {retrieved.Metadata["source"]}");
        _output.WriteLine($"   Version: {retrieved.Metadata["version"]}");
    }

    #endregion

    #region LINQ Query Tests

    [Fact]
    public async Task LinqQuery_Where_ShouldFilter()
    {
        // Arrange
        var hotelCode = "HOTEL_LINQ";
        _context.Orders.AddRange(
            new OrderContext { HotelCode = hotelCode, Status = OrderStatus.Pending, SessionId = Guid.NewGuid().ToString() },
            new OrderContext { HotelCode = hotelCode, Status = OrderStatus.Succeeded, SessionId = Guid.NewGuid().ToString() },
            new OrderContext { HotelCode = hotelCode, Status = OrderStatus.Failed, SessionId = Guid.NewGuid().ToString() }
        );
        await _context.SaveChangesAsync();

        // Act
        var pendingOrders = await _context.Orders
            .WithPartitionKey(hotelCode)
            .Where(o => o.Status == OrderStatus.Pending)
            .ToListAsync();

        // Assert
        pendingOrders.Should().NotBeEmpty();
        pendingOrders.All(o => o.Status == OrderStatus.Pending).Should().BeTrue();
        _output.WriteLine($"? LINQ Where filtered {pendingOrders.Count} pending orders");
    }

    [Fact]
    public async Task LinqQuery_OrderBy_ShouldSort()
    {
        // Arrange
        var hotelCode = "HOTEL_SORT";
        var baseTime = DateTime.UtcNow;
        
        _context.Orders.AddRange(
            new OrderContext { HotelCode = hotelCode, SessionId = "3", CreatedAt = baseTime.AddMinutes(3) },
            new OrderContext { HotelCode = hotelCode, SessionId = "1", CreatedAt = baseTime.AddMinutes(1) },
            new OrderContext { HotelCode = hotelCode, SessionId = "2", CreatedAt = baseTime.AddMinutes(2) }
        );
        await _context.SaveChangesAsync();

        // Act
        var sortedOrders = await _context.Orders
            .WithPartitionKey(hotelCode)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        // Assert
        sortedOrders.Count.Should().BeGreaterThanOrEqualTo(3);
        for (int i = 0; i < sortedOrders.Count - 1; i++)
        {
            sortedOrders[i].CreatedAt.Should().BeOnOrBefore(sortedOrders[i + 1].CreatedAt);
        }
        _output.WriteLine($"? Orders sorted by CreatedAt");
    }

    [Fact]
    public async Task LinqQuery_Count_ShouldReturnCorrectCount()
    {
        // Arrange
        var hotelCode = "HOTEL_COUNT";
        _context.Orders.AddRange(
            new OrderContext { HotelCode = hotelCode, SessionId = Guid.NewGuid().ToString(), Status = OrderStatus.Pending },
            new OrderContext { HotelCode = hotelCode, SessionId = Guid.NewGuid().ToString(), Status = OrderStatus.Pending },
            new OrderContext { HotelCode = hotelCode, SessionId = Guid.NewGuid().ToString(), Status = OrderStatus.Succeeded }
        );
        await _context.SaveChangesAsync();

        // Act
        var totalCount = await _context.Orders
            .WithPartitionKey(hotelCode)
            .CountAsync();
        
        var pendingCount = await _context.Orders
            .WithPartitionKey(hotelCode)
            .CountAsync(o => o.Status == OrderStatus.Pending);

        // Assert
        totalCount.Should().BeGreaterThanOrEqualTo(3);
        pendingCount.Should().BeGreaterThanOrEqualTo(2);
        _output.WriteLine($"? Total: {totalCount}, Pending: {pendingCount}");
    }

    #endregion

    #region Change Tracking Tests

    [Fact]
    public async Task ChangeTracking_ShouldDetectModifications()
    {
        // Arrange
        var order = new OrderContext
        {
            HotelCode = "HOTEL_TRACK",
            SessionId = Guid.NewGuid().ToString(),
            Status = OrderStatus.Pending
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Act - Modify tracked entity
        order.Status = OrderStatus.PaymentInProgress;
        order.CurrentStep = "Payment";

        var entry = _context.Entry(order);
        var isModified = entry.State == EntityState.Modified;

        await _context.SaveChangesAsync();

        // Assert
        isModified.Should().BeTrue();
        
        var retrieved = await _context.Orders
            .WithPartitionKey(order.HotelCode)
            .FirstOrDefaultAsync(o => o.Id == order.Id);
        
        retrieved!.Status.Should().Be(OrderStatus.PaymentInProgress);
        _output.WriteLine("? Change tracking detected modifications");
    }

    [Fact]
    public async Task NoTracking_ShouldNotTrackEntities()
    {
        // Arrange
        var order = new OrderContext
        {
            HotelCode = "HOTEL_NOTRACK",
            SessionId = Guid.NewGuid().ToString(),
            Status = OrderStatus.Pending
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Act
        var noTrackOrder = await _context.Orders
            .WithPartitionKey(order.HotelCode)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == order.Id);

        noTrackOrder!.Status = OrderStatus.Succeeded;

        var entry = _context.Entry(noTrackOrder);
        var state = entry.State;

        // Assert
        state.Should().Be(EntityState.Detached);
        _output.WriteLine("? No-tracking query returned detached entity");
    }

    #endregion

    #region Batch Operations Tests

    [Fact]
    public async Task AddRange_ShouldAddMultipleOrders()
    {
        // Arrange
        var hotelCode = "HOTEL_BATCH";
        var orders = Enumerable.Range(1, 10).Select(i => new OrderContext
        {
            HotelCode = hotelCode,
            SessionId = $"session-{i}",
            Status = OrderStatus.Pending
        }).ToList();

        // Act
        _context.Orders.AddRange(orders);
        await _context.SaveChangesAsync();

        // Assert
        var retrieved = await _context.Orders
            .WithPartitionKey(hotelCode)
            .ToListAsync();

        retrieved.Count.Should().BeGreaterThanOrEqualTo(10);
        _output.WriteLine($"? Batch added {orders.Count} orders");
    }

    [Fact]
    public async Task RemoveRange_ShouldDeleteMultipleOrders()
    {
        // Arrange
        var hotelCode = "HOTEL_BATCH_DEL";
        var orders = Enumerable.Range(1, 5).Select(i => new OrderContext
        {
            HotelCode = hotelCode,
            SessionId = $"session-{i}",
            Status = OrderStatus.Failed
        }).ToList();

        _context.Orders.AddRange(orders);
        await _context.SaveChangesAsync();

        // Act
        _context.Orders.RemoveRange(orders);
        await _context.SaveChangesAsync();

        var remaining = await _context.Orders
            .WithPartitionKey(hotelCode)
            .Where(o => o.Status == OrderStatus.Failed)
            .ToListAsync();

        // Assert
        remaining.Should().BeEmpty();
        _output.WriteLine($"? Batch deleted {orders.Count} orders");
    }

    #endregion

    

    public async Task DisposeAsync()
    {
        try
        {
            _context?.Dispose();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"??  Context cleanup warning: {ex.Message}");
        }

        if (_testHost != null)
        {
            await _testHost.StopAsync();
            _testHost.Dispose();
        }

        if (_cosmosContainer != null)
        {
            await _cosmosContainer.DisposeAsync();
        }
    }
}
