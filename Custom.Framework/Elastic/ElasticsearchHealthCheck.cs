using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Nest;

namespace Custom.Framework.Elastic;

/// <summary>
/// Health check for Elasticsearch cluster connectivity and status
/// </summary>
public class ElasticsearchHealthCheck : IHealthCheck
{
    private readonly IElasticClientFactory _clientFactory;
    private readonly ElasticOptions _options;

    public ElasticsearchHealthCheck(
        IElasticClientFactory clientFactory,
        IOptions<ElasticOptions> options)
    {
        _clientFactory = clientFactory;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.HealthCheckTimeout);

            var client = _clientFactory.CreateClient();
            var pingResponse = await client.PingAsync(ct: cts.Token);

            if (!pingResponse.IsValid)
            {
                return HealthCheckResult.Unhealthy(
                    "Elasticsearch ping failed",
                    pingResponse.OriginalException);
            }

            var clusterHealth = await client.Cluster.HealthAsync(ct: cts.Token);

            if (!clusterHealth.IsValid)
            {
                return HealthCheckResult.Degraded(
                    "Unable to retrieve cluster health",
                    clusterHealth.OriginalException);
            }

            var data = new Dictionary<string, object>
            {
                ["cluster_name"] = clusterHealth.ClusterName,
                ["status"] = clusterHealth.Status.ToString(),
                ["number_of_nodes"] = clusterHealth.NumberOfNodes,
                ["number_of_data_nodes"] = clusterHealth.NumberOfDataNodes,
                ["active_primary_shards"] = clusterHealth.ActivePrimaryShards,
                ["active_shards"] = clusterHealth.ActiveShards,
                ["relocating_shards"] = clusterHealth.RelocatingShards,
                ["initializing_shards"] = clusterHealth.InitializingShards,
                ["unassigned_shards"] = clusterHealth.UnassignedShards
            };

            return clusterHealth.Status.ToString().ToLowerInvariant() switch
            {
                "green" => HealthCheckResult.Healthy("Elasticsearch cluster is healthy", data),
                "yellow" => HealthCheckResult.Degraded("Elasticsearch cluster is in yellow state", null, data),
                "red" => HealthCheckResult.Unhealthy("Elasticsearch cluster is in red state", null, data),
                _ => HealthCheckResult.Unhealthy("Unknown cluster state", null, data)
            };
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Failed to check Elasticsearch health",
                ex);
        }
    }
}
