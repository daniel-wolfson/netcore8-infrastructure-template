using Custom.Framework.Monitoring.Grafana.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Custom.Framework.Monitoring.Grafana;

/// <summary>
/// Interface for Grafana annotation service
/// </summary>
public interface IGrafanaAnnotationService
{
    /// <summary>
    /// Mark a deployment on Grafana graphs
    /// </summary>
    Task MarkDeploymentAsync(string version, string? details = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark an error/incident on Grafana graphs
    /// </summary>
    Task MarkErrorAsync(string message, string? details = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a custom event on Grafana graphs
    /// </summary>
    Task MarkEventAsync(string text, List<string> tags, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for creating annotations in Grafana
/// </summary>
public class GrafanaAnnotationService : IGrafanaAnnotationService
{
    private readonly IGrafanaClient _grafanaClient;
    private readonly GrafanaOptions _options;
    private readonly ILogger<GrafanaAnnotationService> _logger;

    public GrafanaAnnotationService(
        IGrafanaClient grafanaClient,
        IOptions<GrafanaOptions> options,
        ILogger<GrafanaAnnotationService> logger)
    {
        _grafanaClient = grafanaClient ?? throw new ArgumentNullException(nameof(grafanaClient));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task MarkDeploymentAsync(
        string version,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_options.Annotations.EnableDeploymentAnnotations)
            {
                _logger.LogDebug("Deployment annotations are disabled");
                return;
            }

            var text = $"?? Deployment: v{version}";
            if (!string.IsNullOrEmpty(details))
            {
                text += $"\n{details}";
            }

            var tags = new List<string> { "deployment", "release" };
            tags.AddRange(_options.Annotations.DefaultTags);

            var annotation = new GrafanaAnnotation
            {
                DateTime = DateTime.UtcNow,
                Text = text,
                Tags = tags,
                DashboardUid = _options.Annotations.DashboardUid
            };

            await _grafanaClient.CreateAnnotationAsync(annotation, cancellationToken);

            _logger.LogInformation("Deployment annotation created for version {Version}", version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create deployment annotation for version {Version}", version);
            // Don't throw - annotation failures shouldn't break deployments
        }
    }

    public async Task MarkErrorAsync(
        string message,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_options.Annotations.EnableErrorAnnotations)
            {
                _logger.LogDebug("Error annotations are disabled");
                return;
            }

            var text = $"? Error: {message}";
            if (!string.IsNullOrEmpty(details))
            {
                text += $"\n{details}";
            }

            var tags = new List<string> { "error", "incident" };
            tags.AddRange(_options.Annotations.DefaultTags);

            var annotation = new GrafanaAnnotation
            {
                DateTime = DateTime.UtcNow,
                Text = text,
                Tags = tags,
                DashboardUid = _options.Annotations.DashboardUid
            };

            await _grafanaClient.CreateAnnotationAsync(annotation, cancellationToken);

            _logger.LogInformation("Error annotation created: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create error annotation");
            // Don't throw
        }
    }

    public async Task MarkEventAsync(
        string text,
        List<string> tags,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var allTags = new List<string>(tags);
            allTags.AddRange(_options.Annotations.DefaultTags);

            var annotation = new GrafanaAnnotation
            {
                DateTime = DateTime.UtcNow,
                Text = text,
                Tags = allTags,
                DashboardUid = _options.Annotations.DashboardUid
            };

            await _grafanaClient.CreateAnnotationAsync(annotation, cancellationToken);

            _logger.LogInformation("Custom annotation created: {Text}", text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create custom annotation");
            // Don't throw
        }
    }
}
