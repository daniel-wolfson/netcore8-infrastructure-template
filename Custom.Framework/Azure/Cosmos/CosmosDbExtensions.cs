using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Custom.Framework.Azure.Cosmos;

/// <summary>
/// Extension methods for configuring Azure Cosmos DB services
/// </summary>
public static class CosmosDbExtensions
{
    /// <summary>
    /// Add Azure Cosmos DB services for Order management
    /// </summary>
    public static IServiceCollection AddCosmosDbForOrders(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<CosmosDbOptions>? configureOptions = null)
    {
        // Load CosmosDbOptions from configuration
        var options = configuration.GetSection("CosmosDB").Get<CosmosDbOptions>()
            ?? new CosmosDbOptions();

        // Apply optional runtime overrides
        if (configureOptions != null)
        {
            configureOptions(options);
            services.Configure(configureOptions);
        }

        // Register options
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(options);

        // Register DbContext
        services.AddDbContext<OrderDbContext>((serviceProvider, optionsBuilder) =>
        {
            var cosmosOptions = serviceProvider.GetRequiredService<CosmosDbOptions>();
            ConfigureDbContext(optionsBuilder, cosmosOptions);
        });

        // Register repository
        services.AddScoped<IOrderRepository, OrderRepository>();

        // Register initializer
        services.AddScoped<CosmosDbInitializer>();

        return services;
    }

    /// <summary>
    /// Initialize Cosmos DB database and container (for IHost - test scenarios)
    /// </summary>
    public static async Task<IHost> UseCosmosDbAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        using var scope = host.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<CosmosDbInitializer>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CosmosDbInitializer>>();

        try
        {
            if (!await initializer.CanConnectAsync(cancellationToken))
            {
                logger.LogError("⚠️ Cannot connect to Azure Cosmos DB");
                logger.LogWarning("Make sure Cosmos DB emulator is running or connection settings are correct");
                return host;
            }

            var info = await initializer.GetDatabaseInfoAsync(cancellationToken);
            if (!info.DatabaseExists || !info.ContainerExists)
            {
                logger.LogInformation("Initializing Cosmos DB...");
                await initializer.InitializeAsync(cancellationToken);
                
                info = await initializer.GetDatabaseInfoAsync(cancellationToken);
            }

            logger.LogInformation("✅ Cosmos DB initialized");
            logger.LogInformation("  Database: {DatabaseName}", info.DatabaseName);
            logger.LogInformation("  Container: {ContainerName}", info.ContainerName);
            logger.LogInformation("  Partition Key: {PartitionKey}", info.PartitionKeyPath);
            logger.LogInformation("  Default TTL: {Ttl}", info.DefaultTtl);
            
            if (info.AutoscaleMaxThroughput.HasValue)
            {
                logger.LogInformation("  Throughput: Autoscale (Max {MaxRU} RU/s)", info.AutoscaleMaxThroughput.Value);
            }
            else if (info.Throughput.HasValue)
            {
                logger.LogInformation("  Throughput: {Throughput} RU/s", info.Throughput.Value);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Cosmos DB initialization failed: {Message}", ex.Message);
            throw;
        }

        return host;
    }

    /// <summary>
    /// Initialize Cosmos DB database and container (for IApplicationBuilder - web scenarios)
    /// </summary>
    public static IApplicationBuilder UseCosmosDb(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<CosmosDbInitializer>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CosmosDbInitializer>>();

        try
        {
            var canConnect = initializer.CanConnectAsync().GetAwaiter().GetResult();
            if (!canConnect)
            {
                logger.LogError("⚠️ Cannot connect to Azure Cosmos DB");
                logger.LogWarning("Make sure Cosmos DB emulator is running or connection settings are correct");
                return app;
            }

            var info = initializer.GetDatabaseInfoAsync().GetAwaiter().GetResult();
            if (!info.DatabaseExists || !info.ContainerExists)
            {
                logger.LogInformation("Initializing Cosmos DB...");
                initializer.InitializeAsync().GetAwaiter().GetResult();
                
                info = initializer.GetDatabaseInfoAsync().GetAwaiter().GetResult();
            }

            logger.LogInformation("✅ Cosmos DB initialized");
            logger.LogInformation("  Database: {DatabaseName}", info.DatabaseName);
            logger.LogInformation("  Container: {ContainerName}", info.ContainerName);
            logger.LogInformation("  Partition Key: {PartitionKey}", info.PartitionKeyPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Cosmos DB initialization failed: {Message}", ex.Message);
            throw;
        }

        return app;
    }

    private static void ConfigureDbContext(DbContextOptionsBuilder optionsBuilder, CosmosDbOptions options)
    {
        var endpoint = options.GetEndpoint();
        var key = options.GetKey();

        optionsBuilder.UseCosmos(
            accountEndpoint: endpoint,
            accountKey: key,
            databaseName: options.DatabaseName,
            cosmosOptionsAction: cosmosOptions =>
            {
                // Handle SSL certificate validation for Cosmos DB Emulator
                if (options.UseEmulator)
                {
                    cosmosOptions.HttpClientFactory(() =>
                    {
                        HttpMessageHandler httpMessageHandler = new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = (req, cert, chain, errors) => true
                        };
                        return new HttpClient(httpMessageHandler);
                    });
                    
                    // Use Gateway mode for emulator (required for SSL bypass)
                    cosmosOptions.ConnectionMode(Microsoft.Azure.Cosmos.ConnectionMode.Gateway);
                }
                else
                {
                    // Production: Use configured connection mode
                    cosmosOptions.ConnectionMode(options.ConnectionMode == "Direct"
                        ? Microsoft.Azure.Cosmos.ConnectionMode.Direct
                        : Microsoft.Azure.Cosmos.ConnectionMode.Gateway);

                    // These settings only apply to Direct mode
                    if (options.ConnectionMode == "Direct")
                    {
                        cosmosOptions.MaxRequestsPerTcpConnection(options.MaxConcurrentConnections ?? 16);
                        cosmosOptions.MaxTcpConnectionsPerEndpoint(options.MaxConcurrentConnections ?? 16);
                    }
                }

                cosmosOptions.RequestTimeout(TimeSpan.FromSeconds(options.RequestTimeout));

                if (options.ApplicationRegion != null)
                {
                    cosmosOptions.Region(options.ApplicationRegion);
                }
            });

        if (options.EnableDetailedLogging)
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }
    }
}
