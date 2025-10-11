using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Custom.Framework.Aws.AuroraDB;

/// <summary>
/// Helper class for initializing and seeding the Aurora database
/// </summary>
public class AuroraDatabaseInitializer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuroraDatabaseInitializer> _logger;

    public AuroraDatabaseInitializer(
        IServiceProvider serviceProvider,
        ILogger<AuroraDatabaseInitializer> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initialize the database by applying pending migrations
    /// </summary>
    public async Task InitializeAsync(bool seedData = false)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();

            _logger.LogInformation("Starting database initialization...");

            // Apply pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Applying {Count} pending migrations: {Migrations}",
                    pendingMigrations.Count(), string.Join(", ", pendingMigrations));

                await context.Database.MigrateAsync();

                _logger.LogInformation("Migrations applied successfully");
            }
            else
            {
                _logger.LogInformation("Database is up to date, no pending migrations");
            }

            // Seed initial data if requested
            if (seedData)
            {
                await SeedDataAsync(context);
            }

            _logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initializing the database");
            throw;
        }
    }

    /// <summary>
    /// Check if the database can be connected to
    /// </summary>
    public async Task<bool> CanConnectAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();

            return await context.Database.CanConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot connect to database");
            return false;
        }
    }

    /// <summary>
    /// Get database connection information
    /// </summary>
    public async Task<DatabaseInfo> GetDatabaseInfoAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        var canConnect = await context.Database.CanConnectAsync();

        return new DatabaseInfo
        {
            CanConnect = canConnect,
            AppliedMigrations = appliedMigrations.ToList(),
            PendingMigrations = pendingMigrations.ToList(),
            DatabaseProvider = context.Database.ProviderName ?? "Unknown"
        };
    }

    /// <summary>
    /// Seed initial data into the database
    /// </summary>
    private async Task SeedDataAsync(AuroraDbContext context)
    {
        _logger.LogInformation("Seeding initial data...");

        try
        {
            // Check if data already exists
            if (await context.Customers.AnyAsync())
            {
                _logger.LogInformation("Database already contains data, skipping seed");
                return;
            }

            // Seed sample customers
            var customers = new[]
            {
                new Models.Customer
                {
                    Email = "admin@example.com",
                    FirstName = "Admin",
                    LastName = "User",
                    Phone = "+1234567890",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Models.Customer
                {
                    Email = "demo@example.com",
                    FirstName = "Demo",
                    LastName = "User",
                    Phone = "+1234567891",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            context.Customers.AddRange(customers);
            await context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} customers", customers.Length);

            // Seed sample products
            var products = new[]
            {
                new Models.Product
                {
                    Sku = "PROD-001",
                    Name = "Sample Product 1",
                    Description = "This is a sample product",
                    Category = "Electronics",
                    Price = 99.99m,
                    Cost = 50.00m,
                    Quantity = 100,
                    ReservedQuantity = 0,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Models.Product
                {
                    Sku = "PROD-002",
                    Name = "Sample Product 2",
                    Description = "Another sample product",
                    Category = "Books",
                    Price = 29.99m,
                    Cost = 15.00m,
                    Quantity = 50,
                    ReservedQuantity = 0,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            context.Products.AddRange(products);
            await context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} products", products.Length);

            _logger.LogInformation("Initial data seeding completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding data");
            throw;
        }
    }

    /// <summary>
    /// Reset the database (WARNING: Deletes all data!)
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        _logger.LogWarning("Resetting database - this will delete all data!");

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();

        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        _logger.LogInformation("Database reset completed");
    }
}

/// <summary>
/// Database information model
/// </summary>
public class DatabaseInfo
{
    public bool CanConnect { get; set; }
    public List<string> AppliedMigrations { get; set; } = new();
    public List<string> PendingMigrations { get; set; } = new();
    public string DatabaseProvider { get; set; } = string.Empty;

    public bool IsUpToDate => !PendingMigrations.Any();
    public int TotalMigrations => AppliedMigrations.Count + PendingMigrations.Count;
}
