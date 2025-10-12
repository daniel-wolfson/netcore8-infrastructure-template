# Grafana Integration - Quick Start Guide

Get up and running with Grafana integration in 5 minutes!

## Step 1: Install Grafana (Docker)

```bash
docker run -d \
  --name=grafana \
  -p 3001:3000 \
  -e "GF_SECURITY_ADMIN_PASSWORD=admin" \
  grafana/grafana:latest
```

Access Grafana at: http://localhost:3001
- Username: `admin`
- Password: `admin`

## Step 2: Create API Key

1. Go to **Configuration → API Keys** in Grafana UI
2. Click **Add API key**
3. Name: `Custom.Framework`
4. Role: **Editor**
5. Copy the generated key

## Step 3: Add Configuration

Add to `appsettings.json`:

```json
{
  "Grafana": {
    "Enabled": true,
    "Url": "http://localhost:3001",
    "ApiKey": "paste-your-api-key-here",
    "AutoProvisionDataSources": true,
    "AutoProvisionDashboards": true,
    "DataSources": {
      "PrometheusUrl": "http://prometheus:9090"
    }
  },
  "Prometheus": {
    "Enabled": true,
    "MetricsEndpoint": "/metrics"
  }
}
```

## Step 4: Register Services

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

// Map Prometheus metrics endpoint
app.MapPrometheusMetrics();

app.Run();
```

## Step 5: Mark Deployments (Optional)

```csharp
// Inject the annotation service
public class Startup
{
    public void Configure(
        IApplicationBuilder app, 
        IGrafanaAnnotationService grafana)
    {
        // Mark deployment on graphs
        await grafana.MarkDeploymentAsync(
            "1.0.0", 
            "Initial release");
    }
}
```

## Step 6: Run and Verify

1. **Start your application**
   ```bash
   dotnet run
   ```

2. **Check logs** - you should see:
   ```
   [INF] Starting Grafana provisioning...
   [INF] Created Prometheus data source: Prometheus (UID: abc123)
   [INF] Grafana provisioning completed successfully
   ```

3. **Open Grafana** at http://localhost:3000

4. **Verify**:
   - Go to **Configuration → Data Sources**
   - You should see **Prometheus** data source
   - Status should be **Working**

## What Happens Automatically?

✅ **On Application Startup:**
- Checks if Grafana is accessible
- Creates Prometheus data source if it doesn't exist
- Provisions dashboards (if configured)

✅ **During Runtime:**
- Annotation service is ready to mark events
- Dashboard management APIs are available

## Common Issues

### Issue: "Grafana is not accessible"

**Solution**: Verify Grafana is running and URL is correct

```bash
# Test Grafana health
curl http://localhost:3000/api/health
```

### Issue: "Unauthorized" error

**Solution**: Check your API key

```bash
# Test with API key
curl -H "Authorization: Bearer YOUR_API_KEY" \
     http://localhost:3000/api/datasources
```

### Issue: Data source not working

**Solution**: Verify Prometheus URL is accessible from Grafana

```bash
# From Grafana container
docker exec -it grafana curl http://prometheus:9090/api/v1/status/config
```

## Next Steps

1. ✅ **Create Custom Dashboards** - See [README.md](README.md#example-1-manual-dashboard-creation)
2. ✅ **Add Annotations** - Mark deployments and errors
3. ✅ **Provision Custom Templates** - Add your own dashboard JSON files
4. ✅ **Configure Alerts** - Set up alerting rules

## Full Docker Compose Example

```yaml
version: '3.8'

services:
  grafana:
    image: grafana/grafana:latest
    ports:
      - "3001:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - grafana-data:/var/lib/grafana

  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus

  my-app:
    build: .
    ports:
      - "5000:8080"
    environment:
      - Grafana__Url=http://grafana:3001
      - Grafana__ApiKey=${GRAFANA_API_KEY}
      - Prometheus__Enabled=true
    depends_on:
      - grafana
      - prometheus

volumes:
  grafana-data:
  prometheus-data:
```

`prometheus.yml`:
```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'my-app'
    static_configs:
      - targets: ['my-app:8080']
    metrics_path: '/metrics'
```

## Ready to Go! 🚀

Your application now has:
- ✅ Prometheus metrics collection
- ✅ Grafana integration
- ✅ Automatic dashboard provisioning
- ✅ Deployment annotations

Check the [full README](README.md) for advanced features and examples!
