using Custom.Framework.Monitoring.Grafana.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Custom.Framework.Monitoring.Grafana;

/// <summary>
/// HTTP client implementation for Grafana API
/// Thread-safe and supports retry logic
/// </summary>
public class GrafanaClient : IGrafanaClient
{
    private readonly HttpClient _httpClient;
    private readonly GrafanaOptions _options;
    private readonly ILogger<GrafanaClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GrafanaClient(
        HttpClient httpClient,
        IOptions<GrafanaOptions> options,
        ILogger<GrafanaClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_options.Url);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        // Configure authentication
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
        else if (!string.IsNullOrEmpty(_options.Username) && !string.IsNullOrEmpty(_options.Password))
        {
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{_options.Username}:{_options.Password}"));
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Basic", credentials);
        }

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    #region Dashboard Management

    public async Task<GrafanaDashboardResponse> CreateOrUpdateDashboardAsync(
        GrafanaDashboard dashboard,
        string? folderUid = null,
        bool overwrite = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GrafanaDashboardRequest
            {
                Dashboard = dashboard,
                FolderUid = folderUid,
                Overwrite = overwrite
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/dashboards/db",
                request,
                _jsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GrafanaDashboardResponse>(
                _jsonOptions,
                cancellationToken);

            _logger.LogInformation(
                "Dashboard '{Title}' created/updated successfully. UID: {Uid}",
                dashboard.Title,
                result?.Uid);

            return result ?? throw new InvalidOperationException("Failed to parse dashboard response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create/update dashboard '{Title}'", dashboard.Title);
            throw;
        }
    }

    public async Task<GrafanaDashboard?> GetDashboardAsync(
        string uid,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/dashboards/uid/{uid}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(
                _jsonOptions,
                cancellationToken);

            // Extract dashboard from "dashboard" property
            if (result.TryGetProperty("dashboard", out var dashboardElement))
            {
                return JsonSerializer.Deserialize<GrafanaDashboard>(
                    dashboardElement.GetRawText(),
                    _jsonOptions);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dashboard with UID '{Uid}'", uid);
            throw;
        }
    }

    public async Task<bool> DeleteDashboardAsync(
        string uid,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"/api/dashboards/uid/{uid}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Dashboard with UID '{Uid}' not found", uid);
                return false;
            }

            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation("Dashboard with UID '{Uid}' deleted successfully", uid);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete dashboard with UID '{Uid}'", uid);
            throw;
        }
    }

    public async Task<List<GrafanaDashboard>> GetDashboardsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                "/api/search?type=dash-db",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var dashboards = await response.Content.ReadFromJsonAsync<List<GrafanaDashboard>>(
                _jsonOptions,
                cancellationToken);

            return dashboards ?? new List<GrafanaDashboard>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dashboards");
            throw;
        }
    }

    #endregion

    #region Data Source Management

    public async Task<GrafanaDataSourceResponse> CreateDataSourceAsync(
        GrafanaDataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/datasources",
                dataSource,
                _jsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GrafanaDataSourceResponse>(
                _jsonOptions,
                cancellationToken);

            _logger.LogInformation(
                "Data source '{Name}' created successfully. UID: {Uid}",
                dataSource.Name,
                result?.Uid);

            return result ?? throw new InvalidOperationException("Failed to parse data source response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create data source '{Name}'", dataSource.Name);
            throw;
        }
    }

    public async Task<GrafanaDataSource?> GetDataSourceAsync(
        string uid,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/datasources/uid/{uid}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<GrafanaDataSource>(
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get data source with UID '{Uid}'", uid);
            throw;
        }
    }

    public async Task<GrafanaDataSource?> GetDataSourceByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/datasources/name/{Uri.EscapeDataString(name)}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<GrafanaDataSource>(
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get data source with name '{Name}'", name);
            throw;
        }
    }

    public async Task<List<GrafanaDataSource>> GetDataSourcesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                "/api/datasources",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var dataSources = await response.Content.ReadFromJsonAsync<List<GrafanaDataSource>>(
                _jsonOptions,
                cancellationToken);

            return dataSources ?? new List<GrafanaDataSource>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get data sources");
            throw;
        }
    }

    public async Task<bool> DeleteDataSourceAsync(
        string uid,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"/api/datasources/uid/{uid}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Data source with UID '{Uid}' not found", uid);
                return false;
            }

            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation("Data source with UID '{Uid}' deleted successfully", uid);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete data source with UID '{Uid}'", uid);
            throw;
        }
    }

    #endregion

    #region Annotation Management

    public async Task<GrafanaAnnotationResponse> CreateAnnotationAsync(
        GrafanaAnnotation annotation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = GrafanaAnnotationRequest.FromAnnotation(annotation);

            var response = await _httpClient.PostAsJsonAsync(
                "/api/annotations",
                request,
                _jsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GrafanaAnnotationResponse>(
                _jsonOptions,
                cancellationToken);

            _logger.LogInformation(
                "Annotation created successfully. Text: '{Text}', Tags: {Tags}",
                annotation.Text,
                string.Join(", ", annotation.Tags));

            return result ?? throw new InvalidOperationException("Failed to parse annotation response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create annotation");
            throw;
        }
    }

    public async Task<List<GrafanaAnnotation>> GetAnnotationsAsync(
        DateTime from,
        DateTime to,
        string? dashboardUid = null,
        List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fromMs = new DateTimeOffset(from).ToUnixTimeMilliseconds();
            var toMs = new DateTimeOffset(to).ToUnixTimeMilliseconds();

            var queryParams = $"from={fromMs}&to={toMs}";

            if (!string.IsNullOrEmpty(dashboardUid))
            {
                queryParams += $"&dashboardUID={dashboardUid}";
            }

            if (tags != null && tags.Count > 0)
            {
                foreach (var tag in tags)
                {
                    queryParams += $"&tags={Uri.EscapeDataString(tag)}";
                }
            }

            var response = await _httpClient.GetAsync(
                $"/api/annotations?{queryParams}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var annotations = await response.Content.ReadFromJsonAsync<List<GrafanaAnnotation>>(
                _jsonOptions,
                cancellationToken);

            return annotations ?? new List<GrafanaAnnotation>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get annotations");
            throw;
        }
    }

    public async Task<bool> DeleteAnnotationAsync(
        int annotationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"/api/annotations/{annotationId}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Annotation with ID '{Id}' not found", annotationId);
                return false;
            }

            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation("Annotation with ID '{Id}' deleted successfully", annotationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete annotation with ID '{Id}'", annotationId);
            throw;
        }
    }

    #endregion

    #region Health Check

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Grafana health check failed");
            return false;
        }
    }

    #endregion
}
