using Custom.Framework.Aws.AuroraDB;
using Custom.Framework.Aws.AuroraDB.Models;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.AWS;

/// <summary>
/// Integration tests for Aurora DB repository
/// These tests require either a local PostgreSQL/MySQL instance or AWS Aurora credentials
/// </summary>
public class AuroraDBTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly ITestOutputHelper _output = output;
    private IHost _testHost = default!;
    private AuroraDbContext _context = default!;

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:latest")
        .WithName("postgres-db")
        .WithNetwork("postgres_pg-network")
        .WithHostname("postgres")
        .WithUsername("postgres")
        .WithPassword("123456")
        .WithEnvironment("POSTGRES_USER", "postgres")
        .WithEnvironment("POSTGRES_PASSWORD", "123456")
        .WithEnvironment("POSTGRES_DB", "postgres")
        .WithPortBinding(5432, 5432)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(5432))
        .Build();

    public async Task InitializeAsync()
    {
        _testHost = await new HostBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                var baseConfig = config
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile("appsettings.Test.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();
                context.HostingEnvironment.EnvironmentName = "Test";

                var auroraDbConfig = new Dictionary<string, string?>
                {
                    ["AuroraDB:Engine"] = baseConfig["AuroraDB:Engine"] ?? "PostgreSQL",
                    ["AuroraDB:WriteEndpoint"] = "localhost",
                    ["AuroraDB:ReadEndpoint"] = "localhost",
                    ["AuroraDB:Database"] = "auroradb",
                    ["AuroraDB:Username"] = "postgres",
                    ["AuroraDB:Password"] = "123456",
                    ["AuroraDB:Port"] = "5432",
                    ["AuroraDB:MaxPoolSize"] = "100",
                    ["AuroraDB:MinPoolSize"] = "10",
                    ["AuroraDB:ConnectionTimeout"] = "30",
                    ["AuroraDB:CommandTimeout"] = "60",
                    ["AuroraDB:EnableRetryOnFailure"] = "true",
                    ["AuroraDB:MaxRetryCount"] = "3",
                    ["AuroraDB:MaxRetryDelay"] = "30",
                    ["AuroraDB:EnableSensitiveDataLogging"] = "false",
                    ["AuroraDB:EnableDetailedErrors"] = "true",
                    ["AuroraDB:EnableReadReplicas"] = "true",
                    ["AuroraDB:UseSSL"] = "false",
                    ["AuroraDB:SSLMode"] = "Disable",
                    ["AuroraDB:UseIAMAuthentication"] = "false",
                    ["AuroraDB:Region"] = "us-east-1"
                };

                config.AddInMemoryCollection(auroraDbConfig);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(b => b.AddXUnit(_output));
                services.AddAuroraDb(context.Configuration);
                services.AddOptions();
                services.AddScoped<AuroraDatabaseInitializer>();
            })
            .StartAsync();

        await _postgresContainer.StartAsync();

        await _testHost.UseAuroraDbMigrationsAsync(seedData: true);

        _context = _testHost.Services.GetRequiredService<AuroraDbContext>();
    }

    [Fact]
    public async Task InitializeDatabase_ShouldApplyMigrations()
    {
        // Arrange - Reset first
        var initializer = _testHost.Services.GetRequiredService<AuroraDatabaseInitializer>();

        await initializer.ResetDatabaseAsync();

        // Act
        await initializer.InitializeAsync(seedData: true);

        // Assert
        var info = await initializer.GetDatabaseInfoAsync();
        Assert.True(info.CanConnect);
        Assert.True(info.IsUpToDate);
    }

    [Fact]
    public async Task Customer_CRUD_Operations_Work()
    {
        var repository = _testHost.Services.GetRequiredService<IAuroraRepository<Customer>>();

        // Create
        var customer = new Customer
        {
            Email = $"test-{Guid.NewGuid()}@example.com",
            FirstName = "John",
            LastName = "Doe",
            Phone = "+1234567890",
            IsActive = true
        };

        await repository.AddAsync(customer);
        await repository.SaveChangesAsync();

        Assert.True(customer.Id > 0);
        _output.WriteLine($"Created customer with ID: {customer.Id}");

        // Read
        var loaded = await repository.GetByIdAsync(customer.Id);
        Assert.NotNull(loaded);
        Assert.Equal(customer.Email, loaded!.Email);
        Assert.Equal(customer.FirstName, loaded.FirstName);
        _output.WriteLine($"Retrieved customer: {loaded.GetFullName()}");

        // Update
        loaded.Phone = "+0987654321";
        loaded.LoginCount = 5;
        loaded.LastLoginAt = DateTime.UtcNow;
        loaded.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(loaded);
        await repository.SaveChangesAsync();

        var updated = await repository.GetByIdAsync(customer.Id);
        Assert.Equal("+0987654321", updated!.Phone);
        Assert.Equal(5, updated.LoginCount);
        _output.WriteLine($"Updated customer phone to: {updated.Phone}");

        // Delete
        await repository.DeleteAsync(updated);
        await repository.SaveChangesAsync();

        var deleted = await repository.GetByIdAsync(customer.Id);
        Assert.Null(deleted);
        _output.WriteLine("Customer deleted successfully");
    }

    [Fact]
    public async Task Bulk_Insert_Operations_Work()
    {
        var repository = _testHost.Services.GetRequiredService<IAuroraRepository<Customer>>();

        // Create multiple customers
        var customers = Enumerable.Range(1, 50).Select(i => new Customer
        {
            Email = $"bulk-test-{i}-{Guid.NewGuid()}@example.com",
            FirstName = $"FirstName{i}",
            LastName = $"LastName{i}",
            Phone = $"+123456{i:D4}",
            IsActive = true
        }).ToList();

        // Bulk insert
        var insertedCount = await repository.BulkInsertAsync(customers);

        Assert.Equal(50, insertedCount);
        _output.WriteLine($"Bulk inserted {insertedCount} customers");

        // Verify count
        var count = await repository.CountAsync(c => c.Email.StartsWith("bulk-test-"));
        Assert.True(count >= 50);
        _output.WriteLine($"Verified {count} customers in database");

        // Clean up
        await repository.BulkDeleteAsync(c => c.Email.StartsWith("bulk-test-"));
    }

    [Fact]
    public async Task Query_With_Filtering_Works()
    {
        var repository = _testHost.Services.GetRequiredService<IAuroraRepository<Customer>>();

        // Create test customers
        var customers = new[]
        {
            new Customer { Email = "active1@test.com", FirstName = "Active", LastName = "User1", IsActive = true },
            new Customer { Email = "active2@test.com", FirstName = "Active", LastName = "User2", IsActive = true },
            new Customer { Email = "inactive@test.com", FirstName = "Inactive", LastName = "User", IsActive = false }
        };

        await repository.AddRangeAsync(customers);
        await repository.SaveChangesAsync();

        // Query active customers
        var activeCustomers = await repository.FindAsync(c => c.IsActive);
        Assert.True(activeCustomers.Count >= 2);
        _output.WriteLine($"Found {activeCustomers.Count} active customers");

        // Query with FirstOrDefault
        var specific = await repository.FirstOrDefaultAsync(c => c.Email == "active1@test.com");
        Assert.NotNull(specific);
        Assert.Equal("Active", specific!.FirstName);
        _output.WriteLine($"Found specific customer: {specific.Email}");

        // Check existence
        var exists = await repository.AnyAsync(c => c.Email == "active1@test.com");
        Assert.True(exists);

        // Clean up
        await repository.DeleteRangeAsync(customers);
        await repository.SaveChangesAsync();
    }

    [Fact]
    public async Task Complex_Query_With_Includes_Works()
    {
        var customerRepo = _testHost.Services.GetRequiredService<IAuroraRepository<Customer>>();
        var orderRepo = _testHost.Services.GetRequiredService<IAuroraRepository<Order>>();

        // Create customer with orders
        var customer = new Customer
        {
            Email = $"order-test-{Guid.NewGuid()}@example.com",
            FirstName = "Order",
            LastName = "Tester"
        };

        await customerRepo.AddAsync(customer);
        await customerRepo.SaveChangesAsync();

        // Create orders
        var orders = new[]
        {
            new Order
            {
                CustomerId = customer.Id,
                OrderNumber = $"ORD-{Guid.NewGuid():N}",
                TotalAmount = 100.50m,
                Status = OrderStatus.Pending
            },
            new Order
            {
                CustomerId = customer.Id,
                OrderNumber = $"ORD-{Guid.NewGuid():N}",
                TotalAmount = 250.75m,
                Status = OrderStatus.Shipped
            }
        };

        await orderRepo.AddRangeAsync(orders);
        await orderRepo.SaveChangesAsync();

        // Query with navigation properties
        var customerWithOrders = await customerRepo.GetQueryable()
            .Include(c => c.Orders)
            .FirstOrDefaultAsync(c => c.Id == customer.Id);

        Assert.NotNull(customerWithOrders);
        Assert.True(customerWithOrders!.Orders.Count >= 2);
        _output.WriteLine($"Customer has {customerWithOrders.Orders.Count} orders");

        // Calculate total order amount
        var totalAmount = customerWithOrders.Orders.Sum(o => o.TotalAmount);
        _output.WriteLine($"Total order amount: ${totalAmount:F2}");

        // Clean up
        await orderRepo.DeleteRangeAsync(orders);
        await customerRepo.DeleteAsync(customer);
        await customerRepo.SaveChangesAsync();
    }

    [Fact]
    public async Task Transaction_Rollback_Works()
    {
        var repository = _testHost.Services.GetRequiredService<IAuroraRepository<Customer>>();

        var customer = new Customer
        {
            Email = $"transaction-test-{Guid.NewGuid()}@example.com",
            FirstName = "Transaction",
            LastName = "Test"
        };

        // Test transaction rollback
        try
        {
            await repository.ExecuteInTransactionAsync<int>(async () =>
            {
                await repository.AddAsync(customer);
                await repository.SaveChangesAsync();

                // Simulate error
                throw new InvalidOperationException("Simulated transaction error");
            });
        }
        catch (InvalidOperationException)
        {
            // Expected exception
        }

        // Verify customer was not created
        var count = await repository.CountAsync(c => c.Email == customer.Email);
        Assert.Equal(0, count);
        _output.WriteLine("Transaction rollback successful");
    }

    [Fact]
    public async Task Transaction_Commit_Works()
    {
        var customerRepo = _testHost.Services.GetRequiredService<IAuroraRepository<Customer>>();
        var orderRepo = _testHost.Services.GetRequiredService<IAuroraRepository<Order>>();

        var result = await customerRepo.ExecuteInTransactionAsync<(Customer customer, Order order)>(async () =>
        {
            // Create customer
            var customer = new Customer
            {
                Email = $"txn-commit-{Guid.NewGuid()}@example.com",
                FirstName = "Transaction",
                LastName = "Commit"
            };
            await customerRepo.AddAsync(customer);
            await customerRepo.SaveChangesAsync();

            // Create order
            var order = new Order
            {
                CustomerId = customer.Id,
                OrderNumber = $"ORD-{Guid.NewGuid():N}",
                TotalAmount = 99.99m,
                Status = OrderStatus.Pending
            };
            await orderRepo.AddAsync(order);
            await orderRepo.SaveChangesAsync();

            return (customer, order);
        });

        // Verify both were created
        var customer = await customerRepo.GetByIdAsync(result.customer.Id);
        var order = await orderRepo.GetByIdAsync(result.order.Id);

        Assert.NotNull(customer);
        Assert.NotNull(order);
        Assert.Equal(result.customer.Id, order!.CustomerId);
        _output.WriteLine("Transaction commit successful");

        // Clean up
        await orderRepo.DeleteAsync(order);
        await customerRepo.DeleteAsync(customer!);
        await customerRepo.SaveChangesAsync();
    }

    [Fact]
    public async Task Read_Replica_Query_Works()
    {
        var repository = _testHost.Services.GetRequiredService<IAuroraRepository<Customer>>();

        // Create test customer
        var customer = new Customer
        {
            Email = $"replica-test-{Guid.NewGuid()}@example.com",
            FirstName = "Replica",
            LastName = "Test"
        };

        await repository.AddAsync(customer);
        await repository.SaveChangesAsync();

        // Query using read replica (or primary if replica not configured)
        var customers = await repository.GetQueryable(useReadReplica: true)
            .Where(c => c.Email == customer.Email)
            .ToListAsync();

        Assert.NotEmpty(customers);
        _output.WriteLine($"Read replica query returned {customers.Count} result(s)");

        // Clean up
        await repository.DeleteAsync(customer);
        await repository.SaveChangesAsync();
    }

    [Fact]
    public async Task Pagination_Works()
    {
        var repository = _testHost.Services.GetRequiredService<IAuroraRepository<Customer>>();

        // Create test customers
        var customers = Enumerable.Range(1, 25).Select(i => new Customer
        {
            Email = $"page-test-{i}@example.com",
            FirstName = $"User{i}",
            LastName = "Test"
        }).ToList();

        await repository.BulkInsertAsync(customers);

        // Test pagination
        int pageSize = 10;
        int page = 0;
        var pagedResults = await repository.GetQueryable()
            .Where(c => c.Email.StartsWith("page-test-"))
            .OrderBy(c => c.Email)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync();

        Assert.Equal(pageSize, pagedResults.Count);
        _output.WriteLine($"Page {page + 1} returned {pagedResults.Count} results");

        // Clean up
        await repository.BulkDeleteAsync(c => c.Email.StartsWith("page-test-"));
    }

    public Task DisposeAsync()
    {
        // Clean up test data
        try
        {
            //await _context.Database.EnsureDeletedAsync();
            return _postgresContainer.DisposeAsync().AsTask();
        }
        catch
        {
            // Ignore cleanup errors
        }

        _testHost.Dispose();
        return Task.CompletedTask;
    }
}
