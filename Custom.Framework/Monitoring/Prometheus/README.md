# Prometheus Monitoring for Custom.Framework

## Overview

This implementation provides comprehensive Prometheus monitoring capabilities for .NET 8 applications using the Custom.Framework library. It includes:

- **Counter metrics**: Track events (requests, errors, orders processed)
- **Gauge metrics**: Track current values (active connections, queue size, temperature)
- **Histogram metrics**: Track distributions (request durations, response sizes)
- **Summary metrics**: Track distributions with quantiles (latency percentiles)
- **ASP.NET Core metrics**: Automatic HTTP request tracking
- **Runtime metrics**: .NET GC, JIT, ThreadPool monitoring
- **System metrics**: CPU, memory, disk, network monitoring
- **Pushgateway support**: For batch jobs and short-lived services

## Features

### ?? Production-Ready
- Thread-safe singleton metrics manager
- Automatic metric registration and caching
- Error handling that doesn't break your application
- Support for custom labels

### ?? Comprehensive Observability
- HTTP request/response metrics out-of-the-box
- .NET runtime metrics (GC, memory, threads)
- System resource metrics (CPU, disk, network)
- Database query metrics (EF Core integration)
- Custom business metrics

### ?? Easy Integration
- Simple configuration via appsettings.json
- Extension methods for ASP.NET Core
- Minimal code changes required
- Support for multiple applications using the same library

## Quick Start

### Step 1: Install NuGet Packages

The packages are already installed in Custom.Framework:
- `prometheus-net` (v8.2.1)
- `prometheus-net.AspNetCore` (v8.2.1)
- `prometheus-net.DotNetRuntime` (v4.4.0)
- `prometheus-net.SystemMetrics` (v3.0.0)

### Step 2: Configure in appsettings.json

```json
{
  "Prometheus": {
    "Enabled": true,
    "MetricsEndpoint": "/metrics",
    "ApplicationName": "my-application",
    "Environment": "Production",
    "EnableAspNetCoreMetrics": true,
    "EnableProcessMetrics": true,
    "EnableRuntimeMetrics": true,
    "EnableDatabaseMetrics": true,
    "EnableHttpClientMetrics": true,
    "CustomLabels": {
      "team": "backend",
      "version": "1.0.0",
      "datacenter": "us-east-1"
    },
    "HistogramBuckets": [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
  }
}
```

### Step 3: Register Services in Program.cs

```csharp
using Custom.Framework.Monitoring.Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Add Prometheus metrics
builder.Services.AddPrometheusMetrics(builder.Configuration);

// Add your other services
builder.Services.AddControllers();

var app = builder.Build();

// Use Prometheus metrics middleware
app.UsePrometheusMetrics();

// Or use endpoint routing (recommended for .NET 8)
app.UseRouting();
app.MapControllers();
app.MapPrometheusMetrics(); // Exposes /metrics endpoint

app.Run();
```

### Step 4: Use the Metrics Manager

```csharp
using Custom.Framework.Monitoring.Prometheus;

public class OrderService
{
    private readonly IPrometheusMetricsService _metrics;

    public OrderService(IPrometheusMetricsService metrics)
    {
        _metrics = metrics;
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
        // Increment a counter
        _metrics.IncrementCounter("orders_created_total");

        // Track duration of the operation
        return await _metrics.TrackDurationAsync(
            "order_creation_duration_seconds",
            async () =>
            {
                // Your business logic here
                var result = await SaveOrderAsync(order);
                return result;
            });
    }

    public void UpdateInventory(string productId, int quantity)
    {
        // Update a gauge
        _metrics.SetGauge("inventory_level", quantity, productId);
    }
}
```

### Step 5: Access Metrics

Navigate to `http://localhost:5000/metrics` (or your configured endpoint) to see the metrics in Prometheus format.

---

## Configuration Options

### Basic Configuration

| Option | Default | Description |
|--------|---------|-------------|
| `Enabled` | `true` | Enable/disable Prometheus metrics |
| `MetricsEndpoint` | `/metrics` | URL path for metrics endpoint |
| `ApplicationName` | `custom-framework-app` | Application name used in labels |
| `Environment` | `Development` | Environment name (Dev, Staging, Prod) |

### Feature Toggles

| Option | Default | Description |
|--------|---------|-------------|
| `EnableAspNetCoreMetrics` | `true` | Automatic HTTP request tracking |
| `EnableProcessMetrics` | `true` | CPU, memory, thread metrics |
| `EnableRuntimeMetrics` | `true` | .NET GC, JIT, exceptions |
| `EnableDatabaseMetrics` | `true` | EF Core query metrics |
| `EnableHttpClientMetrics` | `true` | Outgoing HTTP call metrics |

### Custom Labels

Add global labels to all metrics:

```json
{
  "Prometheus": {
    "CustomLabels": {
      "team": "platform",
      "service": "order-api",
      "region": "us-west-2",
      "environment": "production"
    }
  }
}
```

### Histogram Buckets

Configure histogram buckets for duration metrics:

```json
{
  "Prometheus": {
    "HistogramBuckets": [
      0.001,  // 1ms
      0.005,  // 5ms
      0.01,   // 10ms
      0.025,  // 25ms
      0.05,   // 50ms
      0.1,    // 100ms
      0.25,   // 250ms
      0.5,    // 500ms
      1,      // 1s
      2.5,    // 2.5s
      5,      // 5s
      10      // 10s
    ]
  }
}
```

---

## Metric Types Explained

### 1. Counter (always increases)

Use for: Counting events, requests, errors, etc.

```csharp
// Create a counter
var counter = _metrics.CreateCounter(
    "http_requests_total",
    "Total number of HTTP requests",
    "method", "endpoint", "status_code");

// Increment the counter
_metrics.IncrementCounter("http_requests_total", "GET", "/api/orders", "200");

// Increment by a specific value
_metrics.IncrementCounter("bytes_processed_total", 1024.0);
```

**Example metrics:**
- `orders_created_total` - Total orders created
- `errors_total` - Total errors encountered
- `messages_published_total` - Total Kafka messages published

### 2. Gauge (can go up and down)

Use for: Current values, snapshots, measurements

```csharp
// Create a gauge
var gauge = _metrics.CreateGauge(
    "active_connections",
    "Number of active database connections");

// Set gauge value
_metrics.SetGauge("active_connections", 42);

// Increment gauge
_metrics.IncrementGauge("queue_size");

// Decrement gauge
_metrics.DecrementGauge("queue_size");
```

**Example metrics:**
- `memory_usage_bytes` - Current memory usage
- `active_users` - Currently logged-in users
- `queue_depth` - Messages waiting in queue

### 3. Histogram (distributions)

Use for: Measuring durations, sizes, distributions

```csharp
// Create a histogram
var histogram = _metrics.CreateHistogram(
    "request_duration_seconds",
    "HTTP request duration in seconds",
    buckets: new[] { 0.1, 0.5, 1, 2.5, 5, 10 },
    labelNames: "method", "endpoint");

// Observe a value
_metrics.ObserveHistogram("request_duration_seconds", 0.234, "GET", "/api/orders");

// Track duration automatically
await _metrics.TrackDurationAsync(
    "database_query_duration_seconds",
    async () => await QueryDatabaseAsync());
```

**Example metrics:**
- `http_request_duration_seconds` - Request latency
- `response_size_bytes` - Response size distribution
- `batch_processing_time_seconds` - Batch job duration

### 4. Summary (distributions with quantiles)

Use for: Percentiles (p50, p90, p95, p99)

```csharp
// Create a summary
var summary = _metrics.CreateSummary(
    "api_latency_seconds",
    "API endpoint latency");

// Observe a value
_metrics.ObserveSummary("api_latency_seconds", 0.123);
```

**Example metrics:**
- `query_latency_seconds` - Database query latency percentiles
- `external_api_duration_seconds` - External API call latency

---

## Usage Examples

### Example 1: API Endpoint Monitoring

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IPrometheusMetricsService _metrics;
    private readonly IOrderService _orderService;

    public OrdersController(
        IPrometheusMetricsService metrics,
        IOrderService orderService)
    {
        _metrics = metrics;
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] OrderRequest request)
    {
        return await _metrics.TrackDurationAsync(
            "order_creation_duration_seconds",
            async () =>
            {
                try
                {
                    var order = await _orderService.CreateOrderAsync(request);
                    
                    _metrics.IncrementCounter("orders_created_total", "success");
                    _metrics.SetGauge("last_order_amount", order.TotalAmount);
                    
                    return Ok(order);
                }
                catch (Exception ex)
                {
                    _metrics.IncrementCounter("orders_created_total", "error");
                    _metrics.IncrementCounter("order_errors_total", ex.GetType().Name);
                    throw;
                }
            },
            "POST", "/api/orders");
    }
}
```

### Example 2: Background Job Monitoring

```csharp
public class OrderProcessingJob : IHostedService
{
    private readonly IPrometheusMetricsService _metrics;
    private readonly IOrderRepository _repository;

    public async Task ProcessOrdersAsync()
    {
        await _metrics.TrackDurationAsync(
            "order_processing_job_duration_seconds",
            async () =>
            {
                var orders = await _repository.GetPendingOrdersAsync();
                
                _metrics.SetGauge("pending_orders_count", orders.Count);
                
                foreach (var order in orders)
                {
                    try
                    {
                        await ProcessOrderAsync(order);
                        _metrics.IncrementCounter("orders_processed_total", "success");
                    }
                    catch (Exception ex)
                    {
                        _metrics.IncrementCounter("orders_processed_total", "error");
                        _metrics.IncrementCounter("order_processing_errors_total", 
                            order.Id.ToString(), ex.GetType().Name);
                    }
                }
            });
    }
}
```

### Example 3: Database Query Monitoring

```csharp
public class CustomerRepository
{
    private readonly IPrometheusMetricsService _metrics;
    private readonly DbContext _dbContext;

    public async Task<Customer> GetCustomerAsync(int id)
    {
        return await _metrics.TrackDurationAsync(
            "database_query_duration_seconds",
            async () =>
            {
                var customer = await _dbContext.Customers
                    .Where(c => c.Id == id)
                    .FirstOrDefaultAsync();
                
                _metrics.IncrementCounter("database_queries_total", "customer", "get_by_id");
                
                if (customer == null)
                {
                    _metrics.IncrementCounter("customer_not_found_total");
                }
                
                return customer;
            },
            "customer", "get_by_id");
    }
}
```

### Example 4: Kafka Message Processing

```csharp
public class OrderEventConsumer
{
    private readonly IPrometheusMetricsService _metrics;

    public async Task ProcessMessageAsync(OrderCreatedEvent message)
    {
        _metrics.IncrementCounter("kafka_messages_received_total", "order_created");
        
        await _metrics.TrackDurationAsync(
            "kafka_message_processing_duration_seconds",
            async () =>
            {
                try
                {
                    await HandleOrderCreatedAsync(message);
                    _metrics.IncrementCounter("kafka_messages_processed_total", "success");
                }
                catch (Exception ex)
                {
                    _metrics.IncrementCounter("kafka_messages_processed_total", "error");
                    _metrics.IncrementCounter("kafka_processing_errors_total", 
                        ex.GetType().Name);
                    throw;
                }
            },
            "order_created");
    }
}
```

### Example 5: Cache Monitoring

```csharp
public class CacheService
{
    private readonly IPrometheusMetricsService _metrics;
    private readonly IMemoryCache _cache;

    public T Get<T>(string key)
    {
        if (_cache.TryGetValue(key, out T value))
        {
            _metrics.IncrementCounter("cache_hits_total", typeof(T).Name);
            return value;
        }
        
        _metrics.IncrementCounter("cache_misses_total", typeof(T).Name);
        return default;
    }

    public void Set<T>(string key, T value, TimeSpan expiration)
    {
        _cache.Set(key, value, expiration);
        _metrics.IncrementCounter("cache_entries_added_total", typeof(T).Name);
        _metrics.SetGauge("cache_expiration_seconds", expiration.TotalSeconds, key);
    }
}
```

---

## Automatic Metrics

### ASP.NET Core Metrics (enabled by default)

When `EnableAspNetCoreMetrics` is true, you automatically get:

- `http_requests_received_total` - Total HTTP requests
- `http_requests_in_progress` - Currently processing requests
- `http_request_duration_seconds` - Request duration histogram
- Labeled by: `method`, `endpoint`, `status_code`

### Runtime Metrics (enabled by default)

When `EnableRuntimeMetrics` is true, you get .NET runtime metrics:

- `dotnet_gc_collection_count_total` - GC collections by generation
- `dotnet_gc_memory_total_available_bytes` - Available memory
- `dotnet_gc_heap_size_bytes` - Heap size by generation
- `dotnet_jit_method_total` - JIT compiled methods
- `dotnet_threadpool_num_threads` - ThreadPool thread count
- `dotnet_exceptions_total` - Exception count by type

### Process Metrics (enabled by default)

When `EnableProcessMetrics` is true, you get system metrics:

- `process_cpu_usage` - CPU usage percentage
- `process_memory_working_set_bytes` - Working set memory
- `process_open_handles` - Open file handles
- `process_num_threads` - Thread count

---

## Pushgateway Integration

For batch jobs and short-lived services that can't be scraped by Prometheus, use Pushgateway.

### Configuration

```json
{
  "Prometheus": {
    "Pushgateway": {
      "Enabled": true,
      "Endpoint": "http://pushgateway:9091",
      "JobName": "batch-order-processor",
      "PushIntervalSeconds": 60,
      "Username": "admin",
      "Password": "secret"
    }
  }
}
```

### Usage

The background service automatically pushes metrics to Pushgateway at the configured interval. No code changes needed!

---

## Prometheus Setup and Scraping

### Docker Compose Example

```yaml
version: '3.8'

services:
  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--web.console.libraries=/etc/prometheus/console_libraries'
      - '--web.console.templates=/etc/prometheus/consoles'

  pushgateway:
    image: prom/pushgateway:latest
    ports:
      - "9091:9091"

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - grafana-storage:/var/lib/grafana

volumes:
  grafana-storage:
```

### prometheus.yml Configuration

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'my-application'
    static_configs:
      - targets: ['host.docker.internal:5000']  # Your application
    metrics_path: '/metrics'
    scrape_interval: 10s

  - job_name: 'pushgateway'
    static_configs:
      - targets: ['pushgateway:9091']
    honor_labels: true
```

### Verify Prometheus is Scraping

1. Navigate to `http://localhost:9090`
2. Go to **Status → Targets**
3. Ensure your application is listed and status is **UP**

---

## Grafana Dashboards

### Example Queries

**HTTP Request Rate:**
```promql
rate(http_requests_received_total[5m])
```

**Request Duration (p95):**
```promql
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))
```

**Error Rate:**
```promql
rate(orders_created_total{result="error"}[5m])
```

**Active Connections:**
```promql
active_connections
```

### Pre-built Dashboards

Import these Grafana dashboard IDs:
- **3662** - Prometheus 2.0 Overview
- **1860** - Node Exporter Full
- **10280** - .NET Core Runtime Metrics

---

## Best Practices

### 1. Metric Naming Convention

Follow Prometheus naming conventions:
- Use snake_case: `http_requests_total`
- Use base unit: `_seconds`, `_bytes`, `_ratio`
- Use `_total` suffix for counters
- Be specific: `orders_created_total` not `orders`

### 2. Label Cardinality

**Good:**
```csharp
_metrics.IncrementCounter("http_requests_total", "GET", "/api/orders", "200");
// Labels: method, endpoint, status_code
// Cardinality: ~100 combinations
```

**Bad:**
```csharp
_metrics.IncrementCounter("http_requests_total", userId, orderId, timestamp);
// High cardinality! Will explode Prometheus memory
```

### 3. Error Handling

Metrics should never break your application:

```csharp
public async Task ProcessOrderAsync(Order order)
{
    try
    {
        // Business logic
        await SaveOrderAsync(order);
        
        // Safe to call - errors are caught internally
        _metrics.IncrementCounter("orders_processed_total");
    }
    catch (Exception ex)
    {
        _metrics.IncrementCounter("order_errors_total");
        throw; // Re-throw business exception
    }
}
```

### 4. Performance Considerations

- Metrics are stored in-memory and are very fast
- Histogram observations are ~1-2μs
- Counter increments are ~500ns
- Use histograms for durations, not summaries (histograms aggregate better)

### 5. Testing Metrics

Always verify your metrics are working:

```bash
# Check metrics endpoint
curl http://localhost:5000/metrics

# Look for your custom metrics
curl http://localhost:5000/metrics | grep "orders_created_total"
```

---

## Troubleshooting

### Metrics Endpoint Returns 404

**Problem:** `/metrics` endpoint not accessible

**Solution:**
```csharp
// Ensure you're using the middleware
app.UsePrometheusMetrics();

// Or endpoint routing
app.MapPrometheusMetrics();
```

### No Metrics Showing Up

**Problem:** Metrics are not being recorded

**Solution:**
1. Check `Enabled: true` in appsettings.json
2. Verify metrics manager is registered in DI
3. Check metrics endpoint manually

### High Memory Usage

**Problem:** Prometheus metrics consuming too much memory

**Solution:**
- Reduce label cardinality
- Avoid user IDs, order IDs in labels
- Use fewer histogram buckets
- Check for metric explosion

### Prometheus Not Scraping

**Problem:** Prometheus can't reach your application

**Solution:**
1. Check firewall rules
2. Verify scrape_configs in prometheus.yml
3. Check Prometheus logs: `docker logs prometheus`
4. Test manually: `curl http://your-app:5000/metrics`

---

## Advanced Usage

### Custom Metric Families

```csharp
public class OrderMetrics
{
    private readonly IPrometheusMetricsService _metrics;
    
    private readonly Counter _ordersCreated;
    private readonly Histogram _orderProcessingTime;
    private readonly Gauge _pendingOrders;

    public OrderMetrics(IPrometheusMetricsService metrics)
    {
        _metrics = metrics;
        
        _ordersCreated = _metrics.CreateCounter(
            "orders_created_total",
            "Total number of orders created",
            "customer_type", "payment_method");
        
        _orderProcessingTime = _metrics.CreateHistogram(
            "order_processing_duration_seconds",
            "Time to process an order",
            labelNames: "order_type");
        
        _pendingOrders = _metrics.CreateGauge(
            "pending_orders_count",
            "Number of orders pending processing");
    }

    public void TrackOrderCreated(string customerType, string paymentMethod)
    {
        _ordersCreated.WithLabels(customerType, paymentMethod).Inc();
    }
}
```

### Middleware for Automatic Tracking

```csharp
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IPrometheusMetricsService _metrics;

    public MetricsMiddleware(RequestDelegate next, IPrometheusMetricsService metrics)
    {
        _next = next;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _metrics.TrackDurationAsync(
            "custom_request_duration_seconds",
            async () => await _next(context),
            context.Request.Method,
            context.Request.Path);
    }
}

// Register in Program.cs
app.UseMiddleware<MetricsMiddleware>();
```

---

## Monitoring Strategy

### Golden Signals (Google SRE)

#### 1. Latency
```promql
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))
```

#### 2. Traffic
```promql
rate(http_requests_total[5m])
```

#### 3. Errors
```promql
rate(http_requests_total{status_code=~"5.."}[5m])
```

#### 4. Saturation
```promql
process_memory_working_set_bytes / process_memory_limit_bytes
```

### RED Method (Requests, Errors, Duration)

- **Rate**: Requests per second
- **Errors**: Error rate
- **Duration**: Latency distribution

### USE Method (Utilization, Saturation, Errors)

For resources:
- **Utilization**: CPU, memory usage %
- **Saturation**: Queue depth, waiting threads
- **Errors**: Error counts

---

## Resources

- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Dashboards](https://grafana.com/grafana/dashboards/)
- [prometheus-net GitHub](https://github.com/prometheus-net/prometheus-net)
- [Metric Types Explained](https://prometheus.io/docs/concepts/metric_types/)
- [Best Practices](https://prometheus.io/docs/practices/naming/)

---

## License

Part of Custom.Framework - NetCore8.Infrastructure
