using Custom.Framework.Monitoring.Grafana.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Custom.Framework.Monitoring.Grafana;

/// <summary>
/// Background service that provisions Grafana dashboards and data sources on application startup
/// </summary>
public class GrafanaProvisioningService : IHostedService
{
    private readonly IGrafanaClient _grafanaClient;
    private readonly GrafanaOptions _options;
    private readonly ILogger<GrafanaProvisioningService> _logger;

    public GrafanaProvisioningService(
        IGrafanaClient grafanaClient,
        IOptions<GrafanaOptions> options,
        ILogger<GrafanaProvisioningService> logger)
    {
        _grafanaClient = grafanaClient ?? throw new ArgumentNullException(nameof(grafanaClient));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Grafana integration is disabled");
            return;
        }

        try
        {
            _logger.LogInformation("Starting Grafana provisioning...");

            // Check if Grafana is accessible
            var isHealthy = await _grafanaClient.HealthCheckAsync(cancellationToken);
            if (!isHealthy)
            {
                _logger.LogWarning("Grafana is not accessible. Skipping provisioning.");
                return;
            }

            // Provision data sources first
            if (_options.AutoProvisionDataSources)
            {
                await ProvisionDataSourcesAsync(cancellationToken);
            }

            // Then provision dashboards
            if (_options.AutoProvisionDashboards)
            {
                await ProvisionDashboardsAsync(cancellationToken);
            }

            _logger.LogInformation("Grafana provisioning completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision Grafana resources");
            // Don't throw - provisioning failures shouldn't prevent app startup
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task ProvisionDataSourcesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Check if Prometheus data source already exists
            var existingDataSource = await _grafanaClient.GetDataSourceByNameAsync(
                _options.DataSources.PrometheusName,
                cancellationToken);

            if (existingDataSource != null)
            {
                _logger.LogInformation(
                    "Data source '{Name}' already exists (UID: {Uid})",
                    _options.DataSources.PrometheusName,
                    existingDataSource.Uid);
                return;
            }

            // Create Prometheus data source
            var dataSource = new GrafanaDataSource
            {
                Name = _options.DataSources.PrometheusName,
                Type = "prometheus",
                Url = _options.DataSources.PrometheusUrl,
                Access = "proxy",
                IsDefault = _options.DataSources.SetPrometheusAsDefault,
                JsonData = new GrafanaDataSourceJsonData
                {
                    HttpMethod = "POST",
                    TimeInterval = _options.DataSources.ScrapeInterval
                }
            };

            var response = await _grafanaClient.CreateDataSourceAsync(dataSource, cancellationToken);

            _logger.LogInformation(
                "Created Prometheus data source: {Name} (UID: {Uid})",
                response.Name,
                response.Uid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision Prometheus data source");
        }
    }

    private async Task ProvisionDashboardsAsync(CancellationToken cancellationToken)
    {
        // Note: In a real implementation, you would load dashboard templates from embedded resources
        // or external JSON files. For now, this is a placeholder that demonstrates the pattern.

        try
        {
            if (_options.Dashboards.ProvisionPrometheus)
            {
                await ProvisionPrometheusDashboardAsync(cancellationToken);
            }

            // Add more dashboard provisioning as needed
            // if (_options.Dashboards.ProvisionKafka)
            // {
            //     await ProvisionKafkaDashboardAsync(cancellationToken);
            // }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision dashboards");
        }
    }

    private async Task ProvisionPrometheusDashboardAsync(CancellationToken cancellationToken)
    {
        // This is a simplified example. In production, you would load this from
        // an embedded resource or external JSON file.
        _logger.LogInformation("Prometheus dashboard provisioning is configured but templates need to be added");
        
        // Example structure (you would load actual dashboard JSON):
        // var dashboard = LoadDashboardTemplate("prometheus-metrics.json");
        // await _grafanaClient.CreateOrUpdateDashboardAsync(dashboard, cancellationToken: cancellationToken);
        
        await Task.CompletedTask;
    }
}
