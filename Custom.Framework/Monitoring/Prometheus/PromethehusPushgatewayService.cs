using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;

namespace Custom.Framework.Monitoring.Prometheus;

/// <summary>
/// Background service that pushes metrics to Prometheus Pushgateway
/// Used for batch jobs and short-lived services that can't be scraped
/// </summary>
public class PromethehusPushgatewayService : BackgroundService
{
    private readonly PrometheusOptions _options;
    private readonly ILogger<PromethehusPushgatewayService> _logger;
    private readonly MetricPusher? _pusher;

    public PromethehusPushgatewayService(
        IOptions<PrometheusOptions> options,
        ILogger<PromethehusPushgatewayService> logger)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_options.Pushgateway?.Enabled == true)
        {
            try
            {
                var pushgatewayOptions = _options.Pushgateway;
                
                _pusher = new MetricPusher(new MetricPusherOptions
                {
                    Endpoint = pushgatewayOptions.Endpoint,
                    Job = pushgatewayOptions.JobName,
                    IntervalMilliseconds = (long)(pushgatewayOptions.PushIntervalSeconds * 1000),
                    AdditionalLabels = _options.CustomLabels.Select(kvp => 
                        Tuple.Create(kvp.Key, kvp.Value))
                });

                _logger.LogInformation(
                    "Prometheus Pushgateway configured: {Endpoint}, Job: {JobName}, Interval: {Interval}s",
                    pushgatewayOptions.Endpoint,
                    pushgatewayOptions.JobName,
                    pushgatewayOptions.PushIntervalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure Prometheus Pushgateway");
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_pusher == null)
        {
            _logger.LogWarning("Pushgateway not configured, service will not run");
            return;
        }

        _logger.LogInformation("Starting Prometheus Pushgateway service");

        try
        {
            _pusher.Start();

            // Keep running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Prometheus Pushgateway service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Prometheus Pushgateway service");
            throw;
        }
        finally
        {
            if (_pusher != null)
            {
                _pusher.Stop();
                
                // Push final metrics before shutdown
                _logger.LogInformation("Prometheus Pushgateway service stopped");
            }
        }
    }

    public override void Dispose()
    {
        _pusher?.Stop();
        base.Dispose();
    }
}
