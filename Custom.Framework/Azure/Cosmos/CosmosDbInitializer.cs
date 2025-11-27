using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace Custom.Framework.Azure.Cosmos;

/// <summary>
/// Initializer for Cosmos DB database and container setup
/// </summary>
public class CosmosDbInitializer
{
    private readonly OrderDbContext _context;
    private readonly CosmosDbOptions _options;
    private readonly ILogger<CosmosDbInitializer> _logger;
    private readonly CosmosClient _cosmosClient;

    public CosmosDbInitializer(
        OrderDbContext context,
        CosmosDbOptions options,
        ILogger<CosmosDbInitializer> logger)
    {
        _context = context;
        _options = options;
        _logger = logger;

        // Create Cosmos client for direct operations
        var clientOptions = new CosmosClientOptions
        {
            RequestTimeout = TimeSpan.FromSeconds(_options.RequestTimeout),
            MaxRetryAttemptsOnRateLimitedRequests = _options.MaxRetryAttempts,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(_options.MaxRetryWaitTimeSeconds),
            ApplicationName = _options.ApplicationName,
            AllowBulkExecution = _options.AllowBulkExecution
        };

        if (_options.ApplicationRegion != null)
        {
            clientOptions.ApplicationRegion = _options.ApplicationRegion;
        }

        // Handle SSL certificate validation for Cosmos DB Emulator
        if (_options.UseEmulator)
        {
            _logger.LogWarning("Using Cosmos DB Emulator - bypassing SSL certificate validation");
            
            // Configure HttpClientFactory to bypass SSL validation for emulator
            clientOptions.HttpClientFactory = () =>
            {
                HttpMessageHandler httpMessageHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (req, cert, chain, errors) =>
                    {
                        // Allow self-signed certificates for emulator
                        return true;
                    }
                };

                return new HttpClient(httpMessageHandler);
            };
            
            // Use Gateway mode for emulator (required for SSL bypass)
            clientOptions.ConnectionMode = Microsoft.Azure.Cosmos.ConnectionMode.Gateway;
        }
        else
        {
            // Production: Use configured connection mode
            clientOptions.ConnectionMode = _options.ConnectionMode == "Direct" 
                ? Microsoft.Azure.Cosmos.ConnectionMode.Direct 
                : Microsoft.Azure.Cosmos.ConnectionMode.Gateway;
        }

        _cosmosClient = new CosmosClient(_options.GetEndpoint(), _options.GetKey(), clientOptions);
    }

    /// <summary>
    /// Initialize database and container with proper configuration
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing Cosmos DB: {DatabaseName}", _options.DatabaseName);

            // Ensure database is created
            await EnsureDatabaseCreatedAsync(cancellationToken);

            // Ensure container with TTL and partitioning is configured
            await EnsureContainerCreatedAsync(cancellationToken);

            _logger.LogInformation("Cosmos DB initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Cosmos DB");
            throw;
        }
    }

    /// <summary>
    /// Ensure database exists using EF Core
    /// </summary>
    private async Task EnsureDatabaseCreatedAsync(CancellationToken cancellationToken)
    {
        await _context.Database.EnsureCreatedAsync(cancellationToken);
        _logger.LogInformation("Database {DatabaseName} created or already exists", _options.DatabaseName);
    }

    /// <summary>
    /// Ensure container exists with proper TTL and throughput configuration
    /// </summary>
    private async Task EnsureContainerCreatedAsync(CancellationToken cancellationToken)
    {
        var database = _cosmosClient.GetDatabase(_options.DatabaseName);

        var containerProperties = new ContainerProperties
        {
            Id = _options.ContainerName,
            PartitionKeyPath = _options.PartitionKeyPath,
            DefaultTimeToLive = -1 // Enable TTL, but use per-document TTL
        };

        // Add composite indexes for efficient querying
        containerProperties.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });
        
        // Add composite indexes
        containerProperties.IndexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>
        {
            new CompositePath { Path = "/hotelCode", Order = CompositePathSortOrder.Ascending },
            new CompositePath { Path = "/status", Order = CompositePathSortOrder.Ascending }
        });

        containerProperties.IndexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>
        {
            new CompositePath { Path = "/hotelCode", Order = CompositePathSortOrder.Ascending },
            new CompositePath { Path = "/sessionId", Order = CompositePathSortOrder.Ascending }
        });

        containerProperties.IndexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>
        {
            new CompositePath { Path = "/hotelCode", Order = CompositePathSortOrder.Ascending },
            new CompositePath { Path = "/createdAt", Order = CompositePathSortOrder.Descending }
        });

        ThroughputProperties? throughputProperties = null;
        if (_options.EnableAutoscale && _options.MaxThroughput.HasValue)
        {
            throughputProperties = ThroughputProperties.CreateAutoscaleThroughput(_options.MaxThroughput.Value);
            _logger.LogInformation("Using autoscale throughput with max {MaxThroughput} RU/s", _options.MaxThroughput.Value);
        }
        else if (_options.MaxThroughput.HasValue)
        {
            throughputProperties = ThroughputProperties.CreateManualThroughput(_options.MaxThroughput.Value);
            _logger.LogInformation("Using manual throughput with {Throughput} RU/s", _options.MaxThroughput.Value);
        }

        var containerResponse = await database.CreateContainerIfNotExistsAsync(
            containerProperties,
            throughputProperties,
            cancellationToken: cancellationToken);

        if (containerResponse.StatusCode == System.Net.HttpStatusCode.Created)
        {
            _logger.LogInformation("Container {ContainerName} created with partition key {PartitionKey}", 
                _options.ContainerName, _options.PartitionKeyPath);
        }
        else
        {
            _logger.LogInformation("Container {ContainerName} already exists", _options.ContainerName);
        }
    }

    /// <summary>
    /// Get database information
    /// </summary>
    public async Task<DatabaseInfo> GetDatabaseInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _cosmosClient.GetDatabase(_options.DatabaseName);
            var container = database.GetContainer(_options.ContainerName);

            var databaseResponse = await database.ReadAsync(cancellationToken: cancellationToken);
            var containerResponse = await container.ReadContainerAsync(cancellationToken: cancellationToken);
            
            int? throughput = null;
            int? maxThroughput = null;
            
            try
            {
                var throughputResponse = await container.ReadThroughputAsync(cancellationToken: cancellationToken);
                throughput = throughputResponse;
                // Note: Autoscale max throughput is not directly available from ReadThroughputAsync
                // It returns the current provisioned throughput
            }
            catch
            {
                // Throughput might not be configured at container level
            }

            return new DatabaseInfo
            {
                DatabaseExists = true,
                ContainerExists = true,
                DatabaseName = _options.DatabaseName,
                ContainerName = _options.ContainerName,
                PartitionKeyPath = containerResponse.Resource.PartitionKeyPath,
                DefaultTtl = containerResponse.Resource.DefaultTimeToLive,
                Throughput = throughput,
                AutoscaleMaxThroughput = maxThroughput
            };
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new DatabaseInfo
            {
                DatabaseExists = false,
                ContainerExists = false,
                DatabaseName = _options.DatabaseName,
                ContainerName = _options.ContainerName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database info");
            throw;
        }
    }

    /// <summary>
    /// Check if can connect to Cosmos DB
    /// </summary>
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var databases = _cosmosClient.GetDatabaseQueryIterator<DatabaseProperties>();
            if (databases.HasMoreResults)
            {
                await databases.ReadNextAsync(cancellationToken);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot connect to Cosmos DB at {Endpoint}", _options.GetEndpoint());
            return false;
        }
    }
}

/// <summary>
/// Database information response
/// </summary>
public class DatabaseInfo
{
    public bool DatabaseExists { get; set; }
    public bool ContainerExists { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string PartitionKeyPath { get; set; } = string.Empty;
    public int? DefaultTtl { get; set; }
    public int? Throughput { get; set; }
    public int? AutoscaleMaxThroughput { get; set; }
}
