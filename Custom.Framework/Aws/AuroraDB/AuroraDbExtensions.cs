using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Custom.Framework.Aws.AuroraDB;

/// <summary>
/// Extension methods for configuring Aurora database services
/// </summary>
public static class AuroraDbExtensions
{
    /// <summary>
    /// Add Aurora database services to the service collection
    /// </summary>
    public static IServiceCollection AddAuroraDb(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AuroraDbOptions>? configureOptions = null)
    {
        // Load AuroraDbOptions from configuration; fall back to defaults if section missing
        var options = configuration.GetSection("AuroraDB").Get<AuroraDbOptions>()
            ?? new AuroraDbOptions();

        // Apply optional runtime overrides and register configure delegate for IOptions<AuroraDbOptions>.
        // The delegate is applied to the created instance and also registered with DI to support consumers
        // that depend on IOptions<AuroraDbOptions>.
        if (configureOptions != null)
        {
            configureOptions(options);
            services.Configure(configureOptions);
        }

        // Register the concrete options instance as a singleton for direct consumption.
        // When configureOptions was provided, IOptions<AuroraDbOptions> is also available.
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(options);

        // Register DbContext
        services.AddDbContext<AuroraDbContext>((serviceProvider, optionsBuilder) =>
        {
            var options = serviceProvider.GetService<AuroraDbOptions>()
                ?? throw new InvalidOperationException("AuroraDbOptions is not registered");
            ConfigureDbContext(optionsBuilder, options);
        });

        // Register repository
        services.AddScoped(typeof(IAuroraRepository<>), 
            typeof(AuroraRepository<>));

        // Register database initializer
        services.AddScoped<AuroraDatabaseInitializer>();

        return services;
    }

    /// <summary>
    /// Initialize Aurora database with migrations (for IHost - test scenarios)
    /// </summary>
    public static async Task<IHost> UseAuroraDbMigrationsAsync(this IHost host, bool seedData = false)
    {
        var initializer = host.Services.GetRequiredService<AuroraDatabaseInitializer>();
        var logger = host.Services.GetRequiredService<ILogger<AuroraDatabaseInitializer>>();

        try
        {
            if (!await initializer.CanConnectAsync())
            {
                logger.LogError("⚠️ Cannot connect to Aurora/PostgreSQL");
                logger.LogWarning("Make sure docker-compose is running:");
                logger.LogWarning("  docker compose -f Cstom.Framework/Aws/LocalStack/docker-compose.yaml up -d aurora-postgres");
            }

            var info = await initializer.GetDatabaseInfoAsync();
            if (!info.DatabaseExist)
            {
                await initializer.EnsureDatabaseExistsAsync();
                await initializer.AddMigrationAsync("InitialCreate");

                info = await initializer.GetDatabaseInfoAsync();
                if (!info.DatabaseExist)
                    return host;
            }

            logger.LogInformation($"DB State: {info.AppliedMigrations.Count} applied, {info.PendingMigrations.Count} pending");

            if (!info.IsUpToDate || info.AppliedMigrations.Count == 0)
            {
                logger.LogInformation("Applying migrations...");
                await initializer.InitializeAsync(seedData);
                logger.LogInformation("✅ Migrations applied and data seeded");
            }
            else
            {
                logger.LogInformation("✅ Database is up to date");
            }
        }
        catch (Npgsql.NpgsqlException ex) when (ex.Message.Contains("does not exist"))
        {
            logger.LogError("⚠️ Database 'auroradb' does not exist. Creating...");
        }
        catch (Exception ex)
        {
            logger.LogError($"❌ Database initialization failed: {ex.Message}");
            throw;
        }
        return host;
    }

    private static void ConfigureDbContext(DbContextOptionsBuilder optionsBuilder, AuroraDbOptions options)
    {
        var connectionString = options.Engine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)
            ? options.BuildPostgreSqlConnectionString()
            : options.BuildMySqlConnectionString();

        if (options.Engine.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            {
                if (options.EnableRetryOnFailure)
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: options.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(options.MaxRetryDelay),
                        errorCodesToAdd: null);
                }
                npgsqlOptions.CommandTimeout(options.CommandTimeout);
                npgsqlOptions.MigrationsAssembly("Custom.Framework");
            });
        }
        else if (options.Engine.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySqlOptions =>
            {
                if (options.EnableRetryOnFailure)
                {
                    mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: options.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(options.MaxRetryDelay),
                        errorNumbersToAdd: null);
                }
                mySqlOptions.CommandTimeout(options.CommandTimeout);
                mySqlOptions.MigrationsAssembly("Custom.Framework");
            });
        }

        if (options.EnableSensitiveDataLogging)
            optionsBuilder.EnableSensitiveDataLogging();

        if (options.EnableDetailedErrors)
            optionsBuilder.EnableDetailedErrors();
    }

}
