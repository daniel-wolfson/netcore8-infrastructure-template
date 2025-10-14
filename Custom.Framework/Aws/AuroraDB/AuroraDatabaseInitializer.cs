using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

namespace Custom.Framework.Aws.AuroraDB;

/// <summary>
/// Helper class for initializing and seeding the Aurora database
/// </summary>
public class AuroraDatabaseInitializer : IDisposable
{
    private IServiceProvider _serviceProvider;
    private bool disposedValue;
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
    /// Programmatically add a migration using dotnet CLI
    /// </summary>
    /// <param name="migrationName">Name of the migration</param>
    /// <param name="projectPath">Path to the project file (optional)</param>
    /// <returns>True if migration was created successfully</returns>
    public async Task<bool> AddMigrationAsync(string migrationName, string? projectPath = null)
    {
        try
        {
            _logger.LogInformation("Creating migration '{MigrationName}'...", migrationName);

            var contextAssembly = typeof(AuroraDbContext).Assembly;
            var assemblyDir = Path.GetDirectoryName(contextAssembly.Location) ?? string.Empty;
            var projectDir = projectPath ?? Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", "Custom.Framework"));
            var projectFile = Path.Combine(projectDir, "Custom.Framework.csproj");

            // Execute: dotnet ef migrations add {migrationName}
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"ef migrations add {migrationName} --project \"{projectFile}\" --context AuroraDbContext --output-dir Aws/AuroraDB/Migrations",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = projectDir
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start migration process");
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Migration '{MigrationName}' created successfully", migrationName);
                _logger.LogDebug("Output: {Output}", output);
                return true;
            }
            else
            {
                _logger.LogError("Failed to create migration '{MigrationName}': {Error}", migrationName, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating migration '{MigrationName}'", migrationName);
            return false;
        }
    }

    /// <summary>
    /// Remove the last migration
    /// </summary>
    /// <param name="projectPath">Path to the project file (optional)</param>
    /// <returns>True if migration was removed successfully</returns>
    public async Task<bool> RemoveLastMigrationAsync(string? projectPath = null)
    {
        try
        {
            _logger.LogWarning("Removing last migration...");

            var workingDirectory = Directory.GetCurrentDirectory();
            projectPath ??= Path.Combine(workingDirectory, "Custom.Framework.csproj");

            // Execute: dotnet ef migrations remove
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"ef migrations remove --project \"{projectPath}\" --context AuroraDbContext --force",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start process");
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Last migration removed successfully");
                _logger.LogDebug("Output: {Output}", output);
                return true;
            }
            else
            {
                _logger.LogError("Failed to remove migration: {Error}", error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing migration");
            return false;
        }
    }

    /// <summary>
    /// Generate SQL script from migrations
    /// </summary>
    /// <param name="fromMigration">Starting migration (optional)</param>
    /// <param name="toMigration">Ending migration (optional)</param>
    /// <returns>SQL script as string</returns>
    public async Task<string?> GenerateMigrationScriptAsync(string? fromMigration = null, string? toMigration = null)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();

            _logger.LogInformation("Generating migration script from '{From}' to '{To}'",
                fromMigration ?? "beginning", toMigration ?? "latest");

            // Generate SQL script for migrations
            var script = context.Database.GenerateCreateScript();

            _logger.LogInformation("Migration script generated successfully");
            return script;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating migration script");
            return null;
        }
    }

    /// <summary>
    /// Check if there are model changes that need a new migration
    /// </summary>
    /// <returns>True if there are pending model changes</returns>
    public async Task<bool> HasPendingModelChangesAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();

            // Check if database has pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Found {Count} pending migrations", pendingMigrations.Count());
                return true;
            }

            // Note: Detecting model changes that haven't been turned into migrations
            // requires EF Core Design tools or comparing the model with the database schema
            _logger.LogInformation("No pending migrations found");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for pending model changes");
            return false;
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

            // CanConnectAsync returns false if server is unreachable, but doesn't check if DB exists
            var canConnect = await context.Database.CanConnectAsync();

            if (!canConnect)
            {
                return false;
            }

            // Actually try to query the database to verify it exists
            // This will throw exception if database doesn't exist
            await context.Database.ExecuteSqlRawAsync("SELECT 1");

            return true;
        }
        catch (Npgsql.NpgsqlException ex) when (ex.SqlState == "3D000") // 3D000 = database does not exist
        {
            _logger.LogWarning("Database does not exist: {Message}", ex.Message);
            return false;
        }
        catch (Npgsql.NpgsqlException ex) when (ex.Message.Contains("does not exist"))
        {
            _logger.LogWarning("Database does not exist: {Message}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot connect to database");
            return false;
        }
    }

    /// <summary>
    /// Check if database exists, and create it if it doesn't
    /// </summary>
    public async Task EnsureDatabaseExistsAsync()
    {
        try
        {
            string databaseName = string.Empty;
            try
            {
                using var scope = _serviceProvider.CreateScope();
                using var context = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();
                databaseName = context.Database.GetDbConnection().Database;

                // Try to connect to the target database
                var rowCount = await context.Database.ExecuteSqlRawAsync("SELECT 1");

                _logger.LogInformation("Database '{Database}' exists", databaseName);
            }
            catch (Npgsql.NpgsqlException ex) when (ex.SqlState == "3D000" || ex.Message.Contains("does not exist"))
            {
                // Database doesn't exist - create it
                _logger.LogWarning("⚠️ Database '{Database}' does not exist. Creating...", databaseName);

                bool created = await CreateDatabaseAsync();
                if (!created)
                {
                    throw new InvalidOperationException($"Failed to create database '{databaseName}'");
                }

            }
            _logger.LogInformation("✅ Database '{Database}' created successfully", databaseName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure database exists");
            throw;
        }
    }

    /// <summary>
    /// Create the database by connecting to the default postgres database
    /// </summary>
    public async Task<bool> CreateDatabaseAsync()
    {
        bool dbCreated = false;
        string targetDatabase = string.Empty;
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var targetOptions = scope.ServiceProvider.GetRequiredService<AuroraDbOptions>();
            targetDatabase = targetOptions.Database;

            // Create a temporary connection to the default 'postgres' database
            var adminOptions = new AuroraDbOptions
            {
                Engine = targetOptions.Engine,
                WriteEndpoint = targetOptions.WriteEndpoint,
                Port = targetOptions.Port,
                Username = targetOptions.Username,
                Password = targetOptions.Password,
                UseSSL = targetOptions.UseSSL,
                SSLMode = targetOptions.SSLMode,
                ConnectionTimeout = targetOptions.ConnectionTimeout,
                UseIAMAuthentication = targetOptions.UseIAMAuthentication
            };

            var connectionString = adminOptions.Engine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)
                ? adminOptions.BuildPostgreSqlConnectionString()
                : adminOptions.BuildMySqlConnectionString();

            var optionsBuilder = new DbContextOptionsBuilder<AuroraDbContext>();

            if (adminOptions.Engine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                optionsBuilder.UseNpgsql(connectionString);
            }
            else if (adminOptions.Engine.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
            {
                optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            }

            using var adminContext = new AuroraDbContext(optionsBuilder.Options, adminOptions);

            // Create the database
            var createDbCommand = adminOptions.Engine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)
                ? $"CREATE DATABASE {targetDatabase}"
                : $"CREATE DATABASE IF NOT EXISTS {targetDatabase}";

            await adminContext.Database.ExecuteSqlRawAsync(createDbCommand);

            dbCreated = true;
            _logger.LogInformation("Database '{Database}' created successfully", targetDatabase);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create database '{Database}'", targetDatabase);
            dbCreated = false;
        }
        return dbCreated;
    }

    /// <summary>
    /// Get database connection information
    /// </summary>
    public async Task<DatabaseInfo> GetDatabaseInfoAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<AuroraDbOptions>();

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        var canConnect = await context.Database.CanConnectAsync();
        bool databaseExist = canConnect;

        if (!canConnect)
        {
            try
            {
                // Try to connect to the target database
                await context.Database.ExecuteSqlRawAsync("SELECT 1");
                _logger.LogInformation("Database '{Database}' exists", options.Database);
                databaseExist = true;
            }
            catch (Npgsql.NpgsqlException ex) when (ex.SqlState == "3D000" || ex.Message.Contains("does not exist"))
            {
                // Database doesn't exist
                _logger.LogWarning("Database '{Database}' does not exist", options.Database);
                databaseExist = false;
            }
        }

        return new DatabaseInfo
        {
            DatabaseExist = databaseExist,
            DatabaseProvider = context.Database.ProviderName ?? "Unknown",
            CanConnect = canConnect,
            AppliedMigrations = [.. appliedMigrations],
            PendingMigrations = [.. pendingMigrations],
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

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();

            await context.Database.EnsureDeletedAsync();
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database reset failed");
            throw;
        }

        _logger.LogInformation("Database reset completed");
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // set large fields to null
            _serviceProvider = null!;
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
