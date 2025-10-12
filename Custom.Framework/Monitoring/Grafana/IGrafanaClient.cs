using Custom.Framework.Monitoring.Grafana.Models;

namespace Custom.Framework.Monitoring.Grafana;

/// <summary>
/// Interface for Grafana HTTP API client
/// Provides methods to interact with Grafana programmatically
/// </summary>
public interface IGrafanaClient
{
    // Dashboard Management
    
    /// <summary>
    /// Create or update a dashboard
    /// </summary>
    Task<GrafanaDashboardResponse> CreateOrUpdateDashboardAsync(
        GrafanaDashboard dashboard, 
        string? folderUid = null,
        bool overwrite = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a dashboard by UID
    /// </summary>
    Task<GrafanaDashboard?> GetDashboardAsync(
        string uid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a dashboard by UID
    /// </summary>
    Task<bool> DeleteDashboardAsync(
        string uid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all dashboards
    /// </summary>
    Task<List<GrafanaDashboard>> GetDashboardsAsync(
        CancellationToken cancellationToken = default);

    // Data Source Management
    
    /// <summary>
    /// Create a new data source
    /// </summary>
    Task<GrafanaDataSourceResponse> CreateDataSourceAsync(
        GrafanaDataSource dataSource,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a data source by UID
    /// </summary>
    Task<GrafanaDataSource?> GetDataSourceAsync(
        string uid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a data source by name
    /// </summary>
    Task<GrafanaDataSource?> GetDataSourceByNameAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all data sources
    /// </summary>
    Task<List<GrafanaDataSource>> GetDataSourcesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a data source by UID
    /// </summary>
    Task<bool> DeleteDataSourceAsync(
        string uid,
        CancellationToken cancellationToken = default);

    // Annotation Management
    
    /// <summary>
    /// Create an annotation (mark an event on graphs)
    /// </summary>
    Task<GrafanaAnnotationResponse> CreateAnnotationAsync(
        GrafanaAnnotation annotation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get annotations within a time range
    /// </summary>
    Task<List<GrafanaAnnotation>> GetAnnotationsAsync(
        DateTime from,
        DateTime to,
        string? dashboardUid = null,
        List<string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an annotation by ID
    /// </summary>
    Task<bool> DeleteAnnotationAsync(
        int annotationId,
        CancellationToken cancellationToken = default);

    // Health Check
    
    /// <summary>
    /// Check if Grafana is accessible
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
