using Elasticsearch.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;

namespace Custom.Framework.Elastic;

/// <summary>
/// Factory interface for creating Elasticsearch clients
/// </summary>
public interface IElasticClientFactory
{
    /// <summary>
    /// Creates or retrieves a singleton Elasticsearch client
    /// </summary>
    IElasticClient CreateClient();

    /// <summary>
    /// Pings the Elasticsearch cluster to verify connectivity
    /// </summary>
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Factory for creating and managing Elasticsearch client instances
/// </summary>
public class ElasticClientFactory : IElasticClientFactory
{
    private readonly ElasticOptions _options;
    private readonly ILogger<ElasticClientFactory> _logger;
    private readonly Lazy<IElasticClient> _client;

    public ElasticClientFactory(
        IOptions<ElasticOptions> options,
        ILogger<ElasticClientFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new Lazy<IElasticClient>(CreateElasticClient);
    }

    public IElasticClient CreateClient() => _client.Value;

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.Value.PingAsync(ct: cancellationToken);
            return response.IsValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ping Elasticsearch cluster");
            return false;
        }
    }

    private IElasticClient CreateElasticClient()
    {
        var nodes = _options.Nodes
            .Select(n => new Uri(n))
            .ToArray();

        IConnectionPool connectionPool = nodes.Length == 1
            ? new SingleNodeConnectionPool(nodes[0])
            : new StaticConnectionPool(nodes);

        var settings = new ConnectionSettings(connectionPool)
            .RequestTimeout(TimeSpan.FromSeconds(_options.RequestTimeout))
            .MaximumRetries(_options.MaxRetries)
            .EnableHttpCompression(_options.EnableCompression)
            .ThrowExceptions(false) // Handle errors gracefully
            .DisableDirectStreaming() // For debugging
            .OnRequestCompleted(details =>
            {
                if (!details.Success)
                {
                    _logger.LogWarning(
                        "Elasticsearch request failed: {Method} {Uri} - {DebugInformation}",
                        details.HttpMethod,
                        details.Uri,
                        details.DebugInformation);
                }
            });

        // Authentication
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            settings.ApiKeyAuthentication(new ApiKeyAuthenticationCredentials(_options.ApiKey));
        }
        else if (!string.IsNullOrEmpty(_options.Username))
        {
            settings.BasicAuthentication(_options.Username, _options.Password);
        }

        return new ElasticClient(settings);
    }
}
