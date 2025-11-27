using Custom.Framework.Azure.Cosmos;
using Custom.Framework.Azure.Cosmos.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Azure.Cosmos;

/// <summary>
/// Integration tests for Azure Cosmos DB Order Repository
/// Tests the complete order lifecycle for hospitality reservation flow
/// </summary>
public class CosmosDbOrderTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IHost _testHost = default!;
    private IOrderRepository _repository = default!;
    private CosmosTestContainer _cosmosContainer = default!;
    private CosmosDbInitializer _initializer = default!;

    public CosmosDbOrderTests(ITestOutputHelper output)
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
                    ["CosmosDB:ContainerName"] = "OrdersTest",
                    ["CosmosDB:PartitionKeyPath"] = "/hotelCode",
                    ["CosmosDB:DefaultTtlSeconds"] = "600",
                    ["CosmosDB:SucceededTtlSeconds"] = "604800",
                    ["CosmosDB:MaxThroughput"] = "4000",
                    ["CosmosDB:EnableAutoscale"] = "true",
                    ["CosmosDB:EnableDetailedLogging"] = "true",
                    ["CosmosDB:DefaultTtl"] = "-1",
                };

                config.AddInMemoryCollection(cosmosConfig);
                context.HostingEnvironment.EnvironmentName = "Test";
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

        _repository = _testHost.Services.GetRequiredService<IOrderRepository>();
        _initializer = _testHost.Services.GetRequiredService<CosmosDbInitializer>();

        _output.WriteLine("? Test host initialized and database ready");
    }

    #region Database Initialization Tests

    [Fact]
    public async Task InitializeDatabase_ShouldCreateDatabaseAndContainer()
    {
        // Act
        var info = await _initializer.GetDatabaseInfoAsync();

        // Assert
        info.DatabaseExists.Should().BeTrue();
        info.ContainerExists.Should().BeTrue();
        info.DatabaseName.Should().Be(_cosmosContainer.DatabaseName);
        info.ContainerName.Should().Be("OrdersTest");
        info.PartitionKeyPath.Should().Be("/hotelCode");
        info.DefaultTtl.Should().Be(-1); // TTL enabled, per-document

        _output.WriteLine($"? Database: {info.DatabaseName}");
        _output.WriteLine($"? Container: {info.ContainerName}");
        _output.WriteLine($"? Partition Key: {info.PartitionKeyPath}");
    }

    [Fact]
    public async Task CanConnect_ShouldReturnTrue()
    {
        // Act
        var canConnect = await _initializer.CanConnectAsync();

        // Assert
        canConnect.Should().BeTrue();
        _output.WriteLine("? Can connect to Cosmos DB");
    }

    #endregion

    #region Order CRUD Tests

    [Fact]
    public async Task CreateOrder_ShouldCreatePendingOrderWithTtl()
    {
        // Arrange
        var order = new OrderContext
        {
            HotelCode = "HOTEL001",
            SessionId = Guid.NewGuid().ToString(),
            Status = OrderStatus.Pending,
            CurrentStep = "SearchHeader",
            OrderData = new OrderData
            {
                CheckInDate = DateTime.Today.AddDays(7),
                CheckOutDate = DateTime.Today.AddDays(10),
                Adults = 2,
                Children = 1,
                Infants = 0
            }
        };

        // Act
        var created = await _repository.CreateOrderAsync(order);

        // Assert
        created.Should().NotBeNull();
        created.Id.Should().NotBeNullOrEmpty();
        created.Status.Should().Be(OrderStatus.Pending);
        created.Ttl.Should().Be(600); // 10 minutes for pending
        created.ExpiresAt.Should().NotBeNull();
        created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _output.WriteLine($"? Created order: {created.Id}");
        _output.WriteLine($"   Status: {created.Status}");
        _output.WriteLine($"   TTL: {created.Ttl} seconds");
        _output.WriteLine($"   Expires: {created.ExpiresAt}");
    }

    [Fact]
    public async Task GetOrderById_ShouldRetrieveOrder()
    {
        // Arrange
        var order = await CreateTestOrderAsync("HOTEL001");

        // Act
        var retrieved = await _repository.GetOrderByIdAsync(order.Id, order.HotelCode);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(order.Id);
        retrieved.HotelCode.Should().Be(order.HotelCode);
        retrieved.SessionId.Should().Be(order.SessionId);

        _output.WriteLine($"? Retrieved order: {retrieved.Id}");
    }

    [Fact]
    public async Task GetOrderBySessionId_ShouldRetrieveMostRecentOrder()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        
        var order1 = await CreateTestOrderAsync("HOTEL001", sessionId);
        await Task.Delay(100); // Small delay to ensure different timestamps
        var order2 = await CreateTestOrderAsync("HOTEL001", sessionId);

        // Act
        var retrieved = await _repository.GetOrderBySessionIdAsync(sessionId, "HOTEL001");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(order2.Id); // Should get most recent
        retrieved.SessionId.Should().Be(sessionId);

        _output.WriteLine($"? Retrieved most recent order by session: {retrieved.Id}");
    }

    [Fact]
    public async Task UpdateOrder_ShouldModifyOrderData()
    {
        // Arrange
        var order = await CreateTestOrderAsync("HOTEL001");

        // Act
        order.CurrentStep = "SelectHeader";
        order.OrderData.RoomCode = "DELUXE";
        order.OrderData.PlanCode = "BB";
        order.OrderData.PriceCode = "STANDARD";
        order.OrderData.TotalAmount = 299.99m;
        order.OrderData.CurrencyCode = "USD";

        var updated = await _repository.UpdateOrderAsync(order);

        // Assert
        updated.Should().NotBeNull();
        updated.CurrentStep.Should().Be("SelectHeader");
        updated.OrderData.RoomCode.Should().Be("DELUXE");
        updated.OrderData.TotalAmount.Should().Be(299.99m);
        updated.UpdatedAt.Should().BeAfter(updated.CreatedAt);

        _output.WriteLine($"? Updated order: {updated.Id}");
        _output.WriteLine($"   Step: {updated.CurrentStep}");
        _output.WriteLine($"   Room: {updated.OrderData.RoomCode}");
        _output.WriteLine($"   Amount: ${updated.OrderData.TotalAmount}");
    }

    [Fact]
    public async Task UpdateOrderStatus_ShouldChangeStatusAndTtl()
    {
        // Arrange
        var order = await CreateTestOrderAsync("HOTEL001");
        var originalTtl = order.Ttl;

        // Act
        var updated = await _repository.UpdateOrderStatusAsync(order.Id, order.HotelCode, OrderStatus.Succeeded);

        // Assert
        updated.Should().NotBeNull();
        updated.Status.Should().Be(OrderStatus.Succeeded);
        updated.Ttl.Should().Be(604800); // 7 days for succeeded
        updated.Ttl.Should().NotBe(originalTtl);

        _output.WriteLine($"? Updated order status: {updated.Id}");
        _output.WriteLine($"   Status: {updated.Status}");
        _output.WriteLine($"   Old TTL: {originalTtl}s ? New TTL: {updated.Ttl}s");
    }

    [Fact]
    public async Task DeleteOrder_ShouldRemoveOrder()
    {
        // Arrange
        var order = await CreateTestOrderAsync("HOTEL001");

        // Act
        var deleted = await _repository.DeleteOrderAsync(order.Id, order.HotelCode);
        var retrieved = await _repository.GetOrderByIdAsync(order.Id, order.HotelCode);

        // Assert
        deleted.Should().BeTrue();
        retrieved.Should().BeNull();

        _output.WriteLine($"? Deleted order: {order.Id}");
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetOrdersByHotel_ShouldReturnAllOrdersForHotel()
    {
        // Arrange
        var hotelCode = "HOTEL999";
        await CreateTestOrderAsync(hotelCode);
        await CreateTestOrderAsync(hotelCode);
        await CreateTestOrderAsync(hotelCode);

        // Act
        var orders = await _repository.GetOrdersByHotelAsync(hotelCode);

        // Assert
        orders.Should().NotBeNull();
        orders.Count.Should().BeGreaterThanOrEqualTo(3);
        orders.All(o => o.HotelCode == hotelCode).Should().BeTrue();

        _output.WriteLine($"? Retrieved {orders.Count} orders for hotel {hotelCode}");
    }

    [Fact]
    public async Task GetOrdersByStatus_ShouldFilterByStatus()
    {
        // Arrange
        var hotelCode = "HOTEL888";
        var pending = await CreateTestOrderAsync(hotelCode);
        var succeeded = await CreateTestOrderAsync(hotelCode);
        await _repository.UpdateOrderStatusAsync(succeeded.Id, succeeded.HotelCode, OrderStatus.Succeeded);

        // Act
        var pendingOrders = await _repository.GetOrdersByStatusAsync(hotelCode, OrderStatus.Pending);
        var succeededOrders = await _repository.GetOrdersByStatusAsync(hotelCode, OrderStatus.Succeeded);

        // Assert
        pendingOrders.Should().Contain(o => o.Id == pending.Id);
        succeededOrders.Should().Contain(o => o.Id == succeeded.Id);

        _output.WriteLine($"? Pending orders: {pendingOrders.Count}");
        _output.WriteLine($"? Succeeded orders: {succeededOrders.Count}");
    }

    [Fact]
    public async Task GetPendingOrders_ShouldReturnOnlyPendingOrders()
    {
        // Arrange
        var hotelCode = "HOTEL777";
        await CreateTestOrderAsync(hotelCode); // Pending by default

        // Act
        var pendingOrders = await _repository.GetPendingOrdersAsync(hotelCode);

        // Assert
        pendingOrders.Should().NotBeEmpty();
        pendingOrders.All(o => o.Status == OrderStatus.Pending).Should().BeTrue();

        _output.WriteLine($"? Retrieved {pendingOrders.Count} pending orders");
    }

    [Fact]
    public async Task OrderExists_ShouldReturnTrueForExistingOrder()
    {
        // Arrange
        var order = await CreateTestOrderAsync("HOTEL001");

        // Act
        var exists = await _repository.OrderExistsAsync(order.Id, order.HotelCode);
        var notExists = await _repository.OrderExistsAsync("non-existent-id", order.HotelCode);

        // Assert
        exists.Should().BeTrue();
        notExists.Should().BeFalse();

        _output.WriteLine($"? Order exists check: {exists}");
    }

    #endregion

    #region Hospitality Flow Tests

    [Fact]
    public async Task CompleteReservationFlow_ShouldUpdateOrderThroughAllSteps()
    {
        // Step 1: Search Header
        var order = new OrderContext
        {
            HotelCode = "HOTEL_FLOW",
            SessionId = Guid.NewGuid().ToString(),
            Status = OrderStatus.Pending,
            CurrentStep = "SearchHeader",
            OrderData = new OrderData
            {
                CheckInDate = DateTime.Today.AddDays(7),
                CheckOutDate = DateTime.Today.AddDays(10),
                Adults = 2,
                Children = 1
            }
        };

        order = await _repository.CreateOrderAsync(order);
        _output.WriteLine($"Step 1 - SearchHeader: {order.Id}");

        // Step 2: Select Header
        order.CurrentStep = "SelectHeader";
        order.OrderData.RoomCode = "DELUXE";
        order.OrderData.PlanCode = "BB";
        order.OrderData.PriceCode = "STANDARD";
        order.OrderData.TotalAmount = 450.00m;
        order.OrderData.CurrencyCode = "USD";
        order = await _repository.UpdateOrderAsync(order);
        _output.WriteLine($"Step 2 - SelectHeader: Room={order.OrderData.RoomCode}, Amount=${order.OrderData.TotalAmount}");

        // Step 3: Reservation Header (Guest Info)
        order.CurrentStep = "ReservationHeader";
        order.OrderData.GuestInfo = new GuestInfo
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "+1234567890",
            Country = "USA"
        };
        order = await _repository.UpdateOrderAsync(order);
        _output.WriteLine($"Step 3 - ReservationHeader: Guest={order.OrderData.GuestInfo.FirstName} {order.OrderData.GuestInfo.LastName}");

        // Step 4: Edge Service Verification
        order.CurrentStep = "EdgeServiceVerification";
        order.VerificationResult = new VerificationResult
        {
            IsVerified = true,
            VerificationTimestamp = DateTime.UtcNow
        };
        order.Status = OrderStatus.PaymentInProgress;
        order = await _repository.UpdateOrderAsync(order);
        _output.WriteLine($"Step 4 - EdgeServiceVerification: Verified={order.VerificationResult.IsVerified}");

        // Step 5: Payment
        order.CurrentStep = "Payment";
        order.PaymentInfo = new PaymentInfo
        {
            PaymentMethod = "CreditCard",
            TransactionId = $"TXN-{Guid.NewGuid():N}",
            Amount = order.OrderData.TotalAmount,
            Currency = order.OrderData.CurrencyCode,
            PaymentStatus = "Success",
            PaidAt = DateTime.UtcNow
        };
        order.Status = OrderStatus.Succeeded;
        order = await _repository.UpdateOrderAsync(order);
        _output.WriteLine($"Step 5 - Payment: TxnId={order.PaymentInfo.TransactionId}, Status={order.PaymentInfo.PaymentStatus}");

        // Step 6: Reservation Summary
        order.CurrentStep = "ReservationSummary";
        order = await _repository.UpdateOrderAsync(order);
        _output.WriteLine($"Step 6 - ReservationSummary: Complete!");

        // Final verification
        var finalOrder = await _repository.GetOrderByIdAsync(order.Id, order.HotelCode);
        finalOrder.Should().NotBeNull();
        finalOrder!.CurrentStep.Should().Be("ReservationSummary");
        finalOrder.Status.Should().Be(OrderStatus.Succeeded);
        finalOrder.Ttl.Should().Be(604800); // 7 days
        finalOrder.OrderData.GuestInfo.Should().NotBeNull();
        finalOrder.PaymentInfo.Should().NotBeNull();
        finalOrder.VerificationResult.Should().NotBeNull();

        _output.WriteLine("? Complete reservation flow executed successfully!");
    }

    [Fact]
    public async Task FailedPaymentFlow_ShouldUpdateStatusToFailed()
    {
        // Arrange
        var order = await CreateTestOrderAsync("HOTEL_FAIL");
        order.CurrentStep = "Payment";
        order.Status = OrderStatus.PaymentInProgress;

        // Act - Simulate failed payment
        order.PaymentInfo = new PaymentInfo
        {
            PaymentMethod = "CreditCard",
            PaymentStatus = "Failed",
            ErrorMessage = "Insufficient funds"
        };
        order.Status = OrderStatus.Failed;
        order = await _repository.UpdateOrderAsync(order);

        // Assert
        order.Status.Should().Be(OrderStatus.Failed);
        order.PaymentInfo!.PaymentStatus.Should().Be("Failed");
        order.Ttl.Should().Be(600); // Short TTL for failed orders

        _output.WriteLine($"? Failed payment flow: {order.PaymentInfo.ErrorMessage}");
        _output.WriteLine($"   TTL set to {order.Ttl}s for cleanup");
    }

    #endregion

    #region TTL Tests

    [Fact]
    public async Task PendingOrder_ShouldHaveShortTtl()
    {
        // Arrange & Act
        var order = await CreateTestOrderAsync("HOTEL001");

        // Assert
        order.Ttl.Should().Be(600); // 10 minutes
        order.ExpiresAt.Should().NotBeNull();
        order.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(600), TimeSpan.FromSeconds(5));

        _output.WriteLine($"? Pending order TTL: {order.Ttl}s (expires at {order.ExpiresAt})");
    }

    [Fact]
    public async Task SucceededOrder_ShouldHaveLongTtl()
    {
        // Arrange
        var order = await CreateTestOrderAsync("HOTEL001");

        // Act
        order.Status = OrderStatus.Succeeded;
        order = await _repository.UpdateOrderAsync(order);

        // Assert
        order.Ttl.Should().Be(604800); // 7 days
        order.ExpiresAt.Should().NotBeNull();
        order.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(604800), TimeSpan.FromSeconds(5));

        _output.WriteLine($"? Succeeded order TTL: {order.Ttl}s ({order.Ttl / 86400} days)");
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task ConcurrentUpdate_ShouldHandleETagConflict()
    {
        // Arrange
        var order = await CreateTestOrderAsync("HOTEL001");
        
        var order1 = await _repository.GetOrderByIdAsync(order.Id, order.HotelCode);
        var order2 = await _repository.GetOrderByIdAsync(order.Id, order.HotelCode);

        // Act
        order1!.OrderData.Adults = 3;
        await _repository.UpdateOrderAsync(order1);

        // Second update should detect concurrency conflict
        order2!.OrderData.Adults = 4;
        
        // Assert
        var updateAction = async () => await _repository.UpdateOrderAsync(order2);
        await updateAction.Should().ThrowAsync<DbUpdateConcurrencyException>();

        _output.WriteLine("? Concurrency conflict detected via ETag");
    }

    #endregion

    #region Helper Methods

    private async Task<OrderContext> CreateTestOrderAsync(string hotelCode, string? sessionId = null)
    {
        var order = new OrderContext
        {
            HotelCode = hotelCode,
            SessionId = sessionId ?? Guid.NewGuid().ToString(),
            Status = OrderStatus.Pending,
            CurrentStep = "SearchHeader",
            OrderData = new OrderData
            {
                CheckInDate = DateTime.Today.AddDays(7),
                CheckOutDate = DateTime.Today.AddDays(10),
                Adults = 2,
                Children = 0,
                Infants = 0
            }
        };

        return await _repository.CreateOrderAsync(order);
    }

    #endregion

    public async Task DisposeAsync()
    {
        try
        {
            // Clean up test data
            var context = _testHost.Services.GetRequiredService<OrderDbContext>();
            
            // Note: Cosmos DB doesn't support Database.EnsureDeleted()
            // Individual cleanup would be needed or let TTL handle it
            _output.WriteLine("??  Test data cleanup: TTL will handle document expiration");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"??  Cleanup warning: {ex.Message}");
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
