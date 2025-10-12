# Prometheus Monitoring - Quick Start Guide

## ✅ Setup Complete!

Your Prometheus monitoring implementation is ready to use! Here's everything you need to get started.

## 📦 What's Included

### Core Files
- `PrometheusOptions.cs` - Configuration options
- `IPrometheusMetricsService.cs` - Metrics manager interface
- `PrometheusMetricsService.cs` - Metrics manager implementation
- `PrometheusExtensions.cs` - ASP.NET Core extensions
- `PromethehusPushgatewayService.cs` - Pushgateway background service
- `README.md` - Comprehensive documentation
- `prometheus.appsettings.json` - Configuration template

### NuGet Packages (Already Installed)
- ✅ `prometheus-net` v8.2.1
- ✅ `prometheus-net.AspNetCore` v8.2.1
- ✅ `prometheus-net.DotNetRuntime` v4.4.0
- ✅ `prometheus-net.SystemMetrics` v3.0.0

### Tests
- `PrometheusTests.cs` - Comprehensive integration tests

---

## 🚀 Quick Start (3 Steps)

### Step 1: Add Configuration

Copy `prometheus.appsettings.json` content to your `appsettings.json`:

```json
{
  "Prometheus": {
    "Enabled": true,
    "MetricsEndpoint": "/metrics",
    "ApplicationName": "my-app",
    "Environment": "Production"
  }
}
```

### Step 2: Register in Program.cs

```csharp
using Custom.Framework.Monitoring.Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Add Prometheus
builder.Services.AddPrometheusMetrics(builder.Configuration);

var app = builder.Build();

// Use Prometheus
app.UseRouting();
app.MapControllers();
app.MapPrometheusMetrics(); // Exposes /metrics endpoint

app.Run();
```

### Step 3: Use in Your Code

```csharp
using Custom.Framework.Monitoring.Prometheus;

public class OrderService
{
    private readonly IPrometheusMetricsService _metrics;

    public OrderService(IPrometheusMetricsService metrics)
    {
        _metrics = metrics;
    }

    public async Task CreateOrderAsync(Order order)
    {
        // Track duration
        await _metrics.TrackDurationAsync(
            "order_creation_duration_seconds",
            async () =>
            {
                await SaveOrderAsync(order);
                _metrics.IncrementCounter("orders_created_total");
            });
    }
}
```

**That's it!** Navigate to `http://localhost:5000/metrics` to see your metrics.

---

## 📊 Metric Types

### Counter (always increases)
```csharp
_metrics.IncrementCounter("requests_total");
_metrics.IncrementCounter("requests_total", "GET", "/api/orders");
```

### Gauge (can go up or down)
```csharp
_metrics.SetGauge("active_connections", 42);
_metrics.IncrementGauge("queue_size");
_metrics.DecrementGauge("queue_size");
```

### Histogram (measure distributions)
```csharp
_metrics.ObserveHistogram("request_duration_seconds", 0.234);

// Or track automatically
await _metrics.TrackDurationAsync(
    "api_call_duration_seconds",
    async () => await CallApiAsync());
```

### Summary (with quantiles)
```csharp
_metrics.ObserveSummary("query_latency_seconds", 0.123);
```

---

## 🔧 Common Scenarios

### API Endpoint Tracking
```csharp
[HttpPost("/api/orders")]
public async Task<IActionResult> CreateOrder([FromBody] OrderRequest request)
{
    return await _metrics.TrackDurationAsync(
        "order_creation_duration_seconds",
        async () =>
        {
            try
            {
                var order = await _orderService.CreateAsync(request);
                _metrics.IncrementCounter("orders_created_total", "success");
                return Ok(order);
            }
            catch (Exception ex)
            {
                _metrics.IncrementCounter("orders_created_total", "error");
                throw;
            }
        },
        "POST", "/api/orders");
}
```

### Database Query Tracking
```csharp
public async Task<Customer> GetCustomerAsync(int id)
{
    return await _metrics.TrackDurationAsync(
        "database_query_duration_seconds",
        async () =>
        {
            var customer = await _dbContext.Customers
                .FirstOrDefaultAsync(c => c.Id == id);
            
            _metrics.IncrementCounter("database_queries_total", "customers");
            return customer;
        },
        "customer", "get_by_id");
}
```

### Background Job Monitoring
```csharp
public class OrderProcessingJob : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _metrics.TrackDurationAsync(
                "job_execution_duration_seconds",
                async () =>
                {
                    var orders = await ProcessOrdersAsync();
                    _metrics.SetGauge("pending_orders", orders.Count);
                    _metrics.IncrementCounter("orders_processed_total", orders.Count);
                },
                "order_processing");

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

---

## 🧪 Run the Tests

```bash
cd Custom.Framework.Tests
dotnet test --filter PrometheusTests
```

**20+ tests** covering all metric types and scenarios.

---

## 🎯 Verify It's Working

### 1. Start Your Application
```bash
dotnet run
```

### 2. Check Metrics Endpoint
```bash
curl http://localhost:5000/metrics
```

You should see:
```
# HELP process_cpu_seconds_total Total user and system CPU time spent in seconds.
# TYPE process_cpu_seconds_total counter
process_cpu_seconds_total 0.47

# HELP http_requests_received_total Total HTTP requests
# TYPE http_requests_received_total counter
http_requests_received_total{code="200",method="GET"} 42
```

### 3. Set Up Prometheus Scraping

Create `prometheus.yml`:
```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'my-application'
    static_configs:
      - targets: ['localhost:5000']
    metrics_path: '/metrics'
```

### 4. Run Prometheus (Docker)
```bash
docker run -p 9090:9090 \
    -v $(pwd)/prometheus.yml:/etc/prometheus/prometheus.yml \
    prom/prometheus
```

Navigate to `http://localhost:9090` and query your metrics!

---

## 📈 Sample Prometheus Queries

```promql
# Request rate per second
rate(http_requests_received_total[5m])

# 95th percentile latency
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))

# Error rate
rate(orders_created_total{status="error"}[5m])

# Active connections
active_connections
```

---

## 🎨 Grafana Dashboard

1. Add Prometheus data source: `http://localhost:9090`
2. Import dashboard ID: `3662` (Prometheus 2.0 Overview)
3. Create custom dashboard with your metrics

---

## 📚 Full Documentation

See [`README.md`](./README.md) for:
- Complete API reference
- Advanced usage patterns
- Best practices
- Troubleshooting
- Production deployment
- Custom metric families
- Middleware examples

---

## 💡 Tips

1. **Keep metric names simple**: `orders_total` not `application_order_service_total_orders_created_count`
2. **Use labels wisely**: Low cardinality (method, status) not high (user_id, order_id)
3. **Choose the right type**: Counter for counts, Gauge for values, Histogram for durations
4. **Don't panic**: Metrics are captured silently - errors won't break your app

---

## 🆘 Need Help?

1. Check `README.md` for detailed documentation
2. Run the tests to see working examples
3. Look at `PrometheusTests.cs` for usage patterns

---

## ✅ You're Ready!

Start tracking metrics in your application now. Happy monitoring! 🎉

```bash
# Quick test
dotnet test --filter PrometheusTests
curl http://localhost:5000/metrics
```
