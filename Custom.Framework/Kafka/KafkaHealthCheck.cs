using Confluent.Kafka;
using Custom.Framework.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class KafkaHealthCheck : IHealthCheck
{
    private readonly KafkaOptions _settings;
    private readonly ILogger _logger;

    public KafkaHealthCheck(KafkaOptions settings, ILogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken token = default)
    {
        try
        {
            using var adminClient = new AdminClientBuilder(new AdminClientConfig 
            { 
                BootstrapServers = _settings.Common.BootstrapServers 
            }).Build();

            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
            
            return metadata.Brokers.Count > 0 
                ? HealthCheckResult.Healthy($"Connected to {metadata.Brokers.Count} brokers")
                : HealthCheckResult.Degraded("No brokers available");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Kafka health check failed");
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}