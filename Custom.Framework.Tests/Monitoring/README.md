# Custom.Framework Monitoring Stack

Docker Compose setup for running **Grafana** and **Prometheus** for monitoring your .NET applications using Custom.Framework.

## ?? What's Included

### Prometheus (Port 9090)
- **Metrics collection** from .NET applications
- **Time-series database** for metrics storage
- **30-day data retention**
- Health check enabled

### Grafana (Port 3001)
- **Metrics visualization** and dashboarding
- **Auto-configured** with Prometheus data source
- **Pre-configured credentials**: `admin` / `Graf1939!`
- Persistent storage for dashboards and settings

## ?? Quick Start

### 1. Start the Monitoring Stack

```bash
# From the Monitoring folder
cd Custom.Framework.Tests/Monitoring

# Start both Grafana and Prometheus
docker-compose -f Monitoring.yaml up -d

# Check status
docker-compose -f Monitoring.yaml ps

# View logs
docker-compose -f Monitoring.yaml logs -f
```

### 2. Access the Services

| Service | URL | Credentials |
|---------|-----|-------------|
| **Grafana** | http://localhost:3001 | Username: `admin`<br>Password: `Graf1939!` |
| **Prometheus** | http://localhost:9090 | No authentication |

### 3. Verify Setup

```bash
# Check Prometheus health
curl http://localhost:9090/-/healthy

# Check Grafana health
curl http://localhost:3001/api/health

# Check Prometheus targets
curl http://localhost:9090/api/v1/targets
```

## ?? Configure Your .NET Application

### Add Prometheus Metrics Endpoint

In your `Program.cs`:

```csharp
using Custom.Framework.Monitoring.Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Add Prometheus metrics
builder.Services.AddPrometheusMetrics(builder.Configuration);

var app = builder.Build();

// Map metrics endpoint
app.MapPrometheusMetrics();

app.Run();
```

### Configure Prometheus to Scrape Your App

Edit `prometheus.yml` and add your application:

```yaml
scrape_configs:
  - job_name: 'my-application'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:5000']  # Your app port
        labels:
          application: 'my-app-name'
          environment: 'development'
```

Then reload Prometheus:

```bash
# Reload configuration without restart
docker-compose -f Monitoring.yaml exec prometheus \
  wget --post-data="" http://localhost:9090/-/reload
```

## ?? View Metrics in Grafana

### 1. Login to Grafana
- Go to http://localhost:3001
- Username: `admin`, Password: `Graf1939!`

### 2. Verify Prometheus Data Source
- Navigate to **Configuration ? Data Sources**
- You should see **Prometheus** already configured and working

### 3. Create a Dashboard
- Click **+ ? Dashboard**
- Click **Add new panel**
- In the query editor, enter a Prometheus query:
  ```promql
  rate(http_requests_received_total[5m])
  ```
- Click **Apply** and **Save**

### 4. Common Prometheus Queries

```promql
# HTTP Request Rate
rate(http_requests_received_total[5m])

# HTTP Error Rate
rate(http_requests_received_total{status_code=~"5.."}[5m])

# Request Duration (p95)
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))

# Active Connections
active_connections

# Memory Usage
process_memory_working_set_bytes
```

## ??? Management Commands

### Start/Stop

```bash
# Start in background
docker-compose -f Monitoring.yaml up -d

# Start with logs
docker-compose -f Monitoring.yaml up

# Stop
docker-compose -f Monitoring.yaml down

# Stop and remove volumes (clears all data)
docker-compose -f Monitoring.yaml down -v
```

### View Logs

```bash
# All logs
docker-compose -f Monitoring.yaml logs -f

# Grafana only
docker-compose -f Monitoring.yaml logs -f grafana

# Prometheus only
docker-compose -f Monitoring.yaml logs -f prometheus
```

### Restart Services

```bash
# Restart all
docker-compose -f Monitoring.yaml restart

# Restart Grafana only
docker-compose -f Monitoring.yaml restart grafana

# Restart Prometheus only
docker-compose -f Monitoring.yaml restart prometheus
```

### Update Configuration

```bash
# After editing prometheus.yml
docker-compose -f Monitoring.yaml restart prometheus

# After editing Grafana provisioning
docker-compose -f Monitoring.yaml restart grafana
```

## ?? File Structure

```
Monitoring/
??? Monitoring.yaml                          # Docker Compose configuration
??? Prometheus/
?   ??? prometheus.yml                       # Prometheus scrape configuration
??? Grafana/
?   ??? provisioning/
?   ?   ??? datasources/
?   ?   ?   ??? prometheus.yml              # Auto-configure Prometheus
?   ?   ??? dashboards/
?   ?       ??? dashboards.yml              # Auto-load dashboards
?   ??? dashboards/
?       ??? (your JSON dashboard files)     # Custom dashboards
??? start-monitoring.bat                    # Quick start script
??? stop-monitoring.bat                     # Stop script
??? cleanup-and-restart.bat                 # Emergency cleanup script
??? diagnose-prometheus.bat                 # Diagnostic tool
??? TROUBLESHOOTING.md                      # Troubleshooting guide
??? README.md                               # This file
```

## ?? Customization

### Change Grafana Password

Edit `Monitoring.yaml`:

```yaml
environment:
  - GF_SECURITY_ADMIN_PASSWORD=YourNewPassword
```

Then recreate the container:

```bash
docker-compose -f Monitoring.yaml down
docker-compose -f Monitoring.yaml up -d
```

### Change Ports

Edit `Monitoring.yaml`:

```yaml
services:
  grafana:
    ports:
      - "3002:3000"  # Map to port 3002 instead

  prometheus:
    ports:
      - "9091:9090"  # Map to port 9091 instead
```

### Add Custom Scrape Targets

Edit `prometheus.yml` and add your targets under `scrape_configs`.

### Configure Data Retention

Edit `Monitoring.yaml` under Prometheus command:

```yaml
command:
  - '--storage.tsdb.retention.time=90d'  # Keep data for 90 days
```

## ?? Integration with Tests

Your `GrafanaTests.cs` is already configured to use these services:

```csharp
private const string GrafanaUrl = "http://localhost:3001";
private const string GrafanaUsername = "admin";
private const string GrafanaPassword = "Graf1939!";
```

Run the tests:

```bash
# Make sure monitoring stack is running
docker-compose -f Monitoring.yaml up -d

# Run Grafana tests
dotnet test --filter "FullyQualifiedName~GrafanaTests"
```

## ?? Pre-Built Dashboards

You can add pre-built dashboard JSON files to `grafana/dashboards/` and they will be automatically loaded.

Example dashboard for .NET applications:

```json
{
  "dashboard": {
    "title": "Custom Framework Metrics",
    "panels": [
      {
        "title": "Request Rate",
        "targets": [
          {
            "expr": "rate(http_requests_received_total[5m])"
          }
        ]
      }
    ]
  }
}
```

## ?? Troubleshooting

### Services Won't Start

```bash
# Check logs
docker-compose -f Monitoring.yaml logs

# Check if ports are already in use
netstat -ano | findstr "3001"
netstat -ano | findstr "9090"
```

### Prometheus Can't Scrape Application

1. **Check targets**: http://localhost:9090/targets
2. **Verify application is exposing /metrics**: `curl http://localhost:5000/metrics`
3. **Use `host.docker.internal`** instead of `localhost` in `prometheus.yml`

### Grafana Can't Connect to Prometheus

1. Check Grafana logs: `docker-compose -f Monitoring.yaml logs grafana`
2. Verify Prometheus is running: `docker-compose -f Monitoring.yaml ps`
3. Test connection: `docker exec custom-framework-grafana wget -O- http://prometheus:9090/-/healthy`

### Reset Everything

```bash
# Stop and remove all data
docker-compose -f Monitoring.yaml down -v

# Start fresh
docker-compose -f Monitoring.yaml up -d
```

## ?? Useful Links

- **Prometheus**: http://localhost:9090
  - Targets: http://localhost:9090/targets
  - Config: http://localhost:9090/config
  - Metrics: http://localhost:9090/graph

- **Grafana**: http://localhost:3001
  - Dashboards: http://localhost:3001/dashboards
  - Data Sources: http://localhost:3001/datasources
  - Explore: http://localhost:3001/explore

## ?? Documentation

- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Documentation](https://grafana.com/docs/grafana/latest/)
- [PromQL Basics](https://prometheus.io/docs/prometheus/latest/querying/basics/)
- [Custom.Framework Prometheus README](../../Custom.Framework/Monitoring/Prometheus/README.md)
- [Custom.Framework Grafana README](../../Custom.Framework/Monitoring/Grafana/README.md)

## ?? Next Steps

1. ? Start the monitoring stack
2. ? Configure your .NET application to expose `/metrics`
3. ? Verify Prometheus is scraping your app
4. ? Login to Grafana and create dashboards
5. ? Use `IGrafanaAnnotationService` to mark deployments
6. ? Run integration tests to verify setup

Happy Monitoring! ????
