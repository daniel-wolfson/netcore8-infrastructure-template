# Grafana Integration for Custom.Framework

## Overview

This library provides seamless integration with Grafana for .NET 8 applications. It enables programmatic management of dashboards, data sources, and annotations through the Grafana HTTP API.

## Features

### ? Core Capabilities
- **Dashboard Management** - Create, update, delete dashboards programmatically
- **Data Source Provisioning** - Automatically configure Prometheus data sources
- **Deployment Annotations** - Mark deployments/releases on graphs automatically
- **Error Tracking** - Annotate incidents and errors
- **Health Monitoring** - Check Grafana availability
- **Retry Logic** - Built-in resilience with Polly

### ? Integration Benefits
- **Infrastructure as Code** - Dashboards defined in code, not manual UI
- **Consistent Setup** - All apps using framework get same monitoring
- **Automated Provisioning** - Dashboards created on app startup
- **Deployment Tracking** - Automatically mark deployments on graphs
- **Version Control** - Dashboard definitions in source control

## Quick Start

### Step 1: Add Configuration

Add to your `appsettings.json`:

```json
{
  "Grafana": {
    "Enabled": true,
    "Url": "http://grafana:3000",
    "ApiKey": "your-grafana-api-key",
    "AutoProvisionDashboards": true,
    "AutoProvisionDataSources": true
  }
}
```

### Step 2: Register Services

In `Program.cs`:

```csharp
using Custom.Framework.Monitoring.Grafana;
using Custom.Framework.Monitoring.Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Add Prometheus metrics
builder.Services.AddPrometheusMetrics(builder.Configuration);

// Add Grafana integration
builder.Services.AddGrafana(builder.Configuration);

var app = builder.Build();

// Expose Prometheus metrics
app.MapPrometheusMetrics();

app.Run();
```

### Step 3: Mark Deployments

```csharp
using Custom.Framework.Monitoring.Grafana;

public class Startup
{
    public void Configure(IApplicationBuilder app, IGrafanaAnnotationService grafana)
    {
        // Mark deployment on Grafana graphs
        await grafana.MarkDeploymentAsync("1.2.3", "Deployed by CI/CD");
        
        app.Run();
    }
}
```

**That's it!** Grafana dashboards and data sources are automatically provisioned on startup.

---

## Configuration

### Basic Configuration

| Option | Default | Description |
|--------|---------|-------------|
| `Enabled` | `true` | Enable/disable Grafana integration |
| `Url` | `http://localhost:3000` | Grafana server URL |
| `ApiKey` | null | Grafana API key (preferred auth method) |
| `Username` | null | Basic auth username |
| `Password` | null | Basic auth password |

### Authentication

**Option 1: API Key (Recommended)**
```json
{
  "Grafana": {
    "ApiKey": "eyJrIjoiT0tTcG1pUlY..."
  }
}
```

Create API key in Grafana:
1. Go to **Configuration ? API Keys**
2. Click **Add API key**
3. Name: `Custom.Framework`
4. Role: `Editor`
5. Copy the key

**Option 2: Basic Authentication**
```json
{
  "Grafana": {
    "Username": "admin",
    "Password": "admin"
  }
}
```

### Dashboard Provisioning

```json
{
  "Grafana": {
    "AutoProvisionDashboards": true,
    "Dashboards": {
      "ProvisionPrometheus": true,
      "ProvisionKafka": true,
      "ProvisionDatabase": true,
      "FolderName": "My Application",
      "Overwrite": true
    }
  }
}
```

### Data Source Provisioning

```json
{
  "Grafana": {
    "AutoProvisionDataSources": true,
    "DataSources": {
      "PrometheusUrl": "http://prometheus:9090",
      "PrometheusName": "Prometheus",
      "SetPrometheusAsDefault": true
    }
  }
}
```

### Annotations

```json
{
  "Grafana": {
    "Annotations": {
      "EnableDeploymentAnnotations": true,
      "EnableErrorAnnotations": true,
      "DefaultTags": ["framework", "production"],
      "DashboardUid": "my-dashboard-uid"
    }
  }
}
```

---

## Usage Examples

### Example 1: Manual Dashboard Creation

```csharp
public class DashboardService
{
    private readonly IGrafanaClient _grafana;

    public async Task CreateMyDashboardAsync()
    {
        var dashboard = new GrafanaDashboard
        {
            Title = "My Application Metrics",
            Tags = new List<string> { "application", "metrics" },
            Panels = new List<GrafanaPanel>
            {
                new GrafanaPanel
                {
                    Id = 1,
                    Title = "Request Rate",
                    Type = "graph",
                    GridPos = new GrafanaGridPos { X = 0, Y = 0, W = 12, H = 8 },
                    Targets = new List<GrafanaTarget>
                    {
                        new GrafanaTarget
                        {
                            Expr = "rate(http_requests_received_total[5m])",
                            LegendFormat = "{{method}} {{endpoint}}"
                        }
                    }
                }
            }
        };

        var response = await _grafana.CreateOrUpdateDashboardAsync(dashboard);
        Console.WriteLine($"Dashboard URL: {response.Url}");
    }
}
```

### Example 2: Create Prometheus Data Source

```csharp
public class DataSourceService
{
    private readonly IGrafanaClient _grafana;

    public async Task SetupPrometheusAsync()
    {
        var dataSource = new GrafanaDataSource
        {
            Name = "Prometheus",
            Type = "prometheus",
            Url = "http://prometheus:9090",
            Access = "proxy",
            IsDefault = true,
            JsonData = new GrafanaDataSourceJsonData
            {
                HttpMethod = "POST",
                TimeInterval = "15s"
            }
        };

        var response = await _grafana.CreateDataSourceAsync(dataSource);
        Console.WriteLine($"Data source created: {response.Name} (UID: {response.Uid})");
    }
}
```

### Example 3: Deployment Annotations

```csharp
// Mark deployment in your CI/CD pipeline
public class DeploymentService
{
    private readonly IGrafanaAnnotationService _annotations;

    public async Task DeployAsync(string version)
    {
        // Your deployment logic...
        await DeployApplicationAsync(version);

        // Mark on Grafana
        await _annotations.MarkDeploymentAsync(
            version,
            $"Deployed by CI/CD\nCommit: {gitCommit}\nAuthor: {author}");
    }
}
```

### Example 4: Error Annotations

```csharp
public class ErrorHandler
{
    private readonly IGrafanaAnnotationService _annotations;

    public async Task HandleErrorAsync(Exception ex)
    {
        // Log error
        _logger.LogError(ex, "Critical error occurred");

        // Mark on Grafana
        await _annotations.MarkErrorAsync(
            ex.Message,
            $"Stack Trace:\n{ex.StackTrace}");
    }
}
```

### Example 5: Custom Events

```csharp
public class EventService
{
    private readonly IGrafanaAnnotationService _annotations;

    public async Task MarkMaintenanceWindowAsync()
    {
        await _annotations.MarkEventAsync(
            "?? Scheduled Maintenance: Database migration",
            new List<string> { "maintenance", "database" });
    }
}
```

---

## API Reference

### IGrafanaClient

#### Dashboard Methods
- `CreateOrUpdateDashboardAsync()` - Create or update a dashboard
- `GetDashboardAsync(uid)` - Get dashboard by UID
- `DeleteDashboardAsync(uid)` - Delete dashboard
- `GetDashboardsAsync()` - Get all dashboards

#### Data Source Methods
- `CreateDataSourceAsync()` - Create a data source
- `GetDataSourceAsync(uid)` - Get data source by UID
- `GetDataSourceByNameAsync(name)` - Get data source by name
- `GetDataSourcesAsync()` - Get all data sources
- `DeleteDataSourceAsync(uid)` - Delete data source

#### Annotation Methods
- `CreateAnnotationAsync()` - Create an annotation
- `GetAnnotationsAsync(from, to)` - Get annotations in time range
- `DeleteAnnotationAsync(id)` - Delete annotation

#### Health
- `HealthCheckAsync()` - Check if Grafana is accessible

### IGrafanaAnnotationService

- `MarkDeploymentAsync(version)` - Mark deployment on graphs
- `MarkErrorAsync(message)` - Mark error/incident
- `MarkEventAsync(text, tags)` - Mark custom event

---

## Docker Compose Setup

```yaml
version: '3.8'

services:
  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
      - GF_USERS_ALLOW_SIGN_UP=false
    volumes:
      - grafana-storage:/var/lib/grafana

  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-storage:/prometheus

  my-application:
    build: .
    ports:
      - "5000:8080"
    environment:
      - Grafana__Url=http://grafana:3000
      - Grafana__ApiKey=${GRAFANA_API_KEY}
      - Prometheus__Enabled=true
    depends_on:
      - grafana
      - prometheus

volumes:
  grafana-storage:
  prometheus-storage:
```

---

## Troubleshooting

### Problem: "Unauthorized" error

**Solution**: Check your API key or credentials

```bash
# Test Grafana API manually
curl -H "Authorization: Bearer YOUR_API_KEY" \
     http://grafana:3000/api/health
```

### Problem: Dashboards not provisioning

**Solution**: Check logs and ensure auto-provisioning is enabled

```csharp
// Enable detailed logging
builder.Logging.AddFilter("Custom.Framework.Monitoring.Grafana", LogLevel.Debug);
```

### Problem: Can't connect to Grafana

**Solution**: Verify Grafana URL and network connectivity

```csharp
var grafana = app.Services.GetRequiredService<IGrafanaClient>();
var healthy = await grafana.HealthCheckAsync();
Console.WriteLine($"Grafana is {(healthy ? "healthy" : "unhealthy")}");
```

---

## Best Practices

### 1. Use API Keys (not basic auth)
```json
{
  "Grafana": {
    "ApiKey": "eyJrIjoiT0tTcG1pUlY...",
    "Username": null,
    "Password": null
  }
}
```

### 2. Store Secrets Securely
```bash
# Use environment variables or Azure Key Vault
export Grafana__ApiKey="your-secret-key"
```

### 3. Version Control Dashboards
- Store dashboard JSON in source control
- Automate provisioning in CI/CD
- Track changes over time

### 4. Mark Important Events
```csharp
// Deployments
await grafana.MarkDeploymentAsync("1.2.3");

// Incidents
await grafana.MarkErrorAsync("Database connection lost");

// Maintenance
await grafana.MarkEventAsync("Maintenance window", new[] { "maintenance" });
```

### 5. Don't Break on Grafana Failures
The library handles errors gracefully - annotation failures won't break your application.

---

## Sample Queries for Dashboards

### Request Rate
```promql
rate(http_requests_received_total[5m])
```

### Error Rate
```promql
rate(http_requests_received_total{status_code=~"5.."}[5m])
```

### Request Duration (p95)
```promql
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))
```

### Active Connections
```promql
active_database_connections
```

### Memory Usage
```promql
process_memory_working_set_bytes
```

---

## Resources

- [Grafana HTTP API Documentation](https://grafana.com/docs/grafana/latest/http_api/)
- [Prometheus + Grafana Setup](https://prometheus.io/docs/visualization/grafana/)
- [Dashboard Best Practices](https://grafana.com/docs/grafana/latest/best-practices/best-practices-for-creating-dashboards/)

---

## License

Part of Custom.Framework - NetCore8.Infrastructure
