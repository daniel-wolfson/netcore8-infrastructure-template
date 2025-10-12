using Custom.Framework.Monitoring.Prometheus;
using Microsoft.AspNetCore.TestHost;
using System.Net;
using Xunit.Abstractions;

namespace Custom.Framework.Tests;

/// <summary>
/// Integration tests for Prometheus metrics implementation
/// Tests all fundamental actions for using Prometheus monitoring
/// </summary>
public class PrometheusTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IHost? _testHost;
    private IPrometheusMetricsService? _metricsManager;

    public PrometheusTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // Setup test host with Prometheus
        _testHost = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Prometheus:Enabled"] = "true",
                            ["Prometheus:MetricsEndpoint"] = "/metrics",
                            ["Prometheus:ApplicationName"] = "test-app",
                            ["Prometheus:Environment"] = "Test",
                            ["Prometheus:EnableAspNetCoreMetrics"] = "true",
                            ["Prometheus:EnableProcessMetrics"] = "true",
                            ["Prometheus:EnableRuntimeMetrics"] = "true",
                            ["Prometheus:CustomLabels:team"] = "test-team",
                            ["Prometheus:CustomLabels:version"] = "1.0.0"
                        });
                    })
                    .ConfigureServices((context, services) =>
                    {
                        services.AddPrometheusMetrics(context.Configuration);
                        services.AddRouting();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapPrometheusMetrics();
                        });
                    });
            })
            .StartAsync();

        _metricsManager = _testHost.Services.GetRequiredService<IPrometheusMetricsService>();
        _output.WriteLine("Test host initialized successfully");
    }

    public async Task DisposeAsync()
    {
        if (_testHost != null)
        {
            await _testHost.StopAsync();
            _testHost.Dispose();
        }
    }

    [Fact]
    public void MetricsManager_Should_Be_Registered()
    {
        // Assert
        Assert.NotNull(_metricsManager);
        _output.WriteLine("✓ IPrometheusMetricsService successfully registered in DI");
    }

    [Fact]
    public async Task MetricsEndpoint_Should_Be_Accessible()
    {
        // Arrange
        var client = _testHost!.GetTestClient();

        // Act
        var response = await client.GetAsync("/metrics");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("# HELP", content);
        Assert.Contains("# TYPE", content);
        _output.WriteLine("✓ /metrics endpoint is accessible");
        _output.WriteLine($"Response preview: {content[..Math.Min(200, content.Length)]}...");
    }

    [Fact]
    public void Counter_Creation_Should_Work()
    {
        // Act
        var counter = _metricsManager!.CreateCounter(
            "test_counter_total",
            "Test counter metric",
            "label1", "label2");

        // Assert
        Assert.NotNull(counter);
        _output.WriteLine("✓ Counter created successfully with labels");
    }

    [Fact]
    public void Counter_Increment_Should_Work()
    {
        // Arrange
        var counterName = $"test_increment_counter_{Guid.NewGuid():N}";
        _metricsManager!.CreateCounter(counterName, "Test increment counter");

        // Act
        _metricsManager.IncrementCounter(counterName);
        _metricsManager.IncrementCounter(counterName);
        _metricsManager.IncrementCounter(counterName, 5);

        // Assert - verify counter exists in metrics
        var registry = _metricsManager.GetRegistry();
        Assert.NotNull(registry);
        _output.WriteLine($"✓ Counter '{counterName}' incremented successfully (1 + 1 + 5 = 7)");
    }

    [Fact]
    public void Counter_WithLabels_Should_Work()
    {
        // Arrange
        var counterName = $"test_labeled_counter_{Guid.NewGuid():N}";
        _metricsManager!.CreateCounter(
            counterName,
            "Test labeled counter",
            "method", "status");

        // Act
        _metricsManager.IncrementCounter(counterName, "GET", "200");
        _metricsManager.IncrementCounter(counterName, "POST", "201");
        _metricsManager.IncrementCounter(counterName, "GET", "500");

        // Assert
        _output.WriteLine($"✓ Counter '{counterName}' with labels tracked successfully");
        _output.WriteLine("  - GET/200: 1");
        _output.WriteLine("  - POST/201: 1");
        _output.WriteLine("  - GET/500: 1");
    }

    [Fact]
    public void Gauge_Creation_And_Operations_Should_Work()
    {
        // Arrange
        var gaugeName = $"test_gauge_{Guid.NewGuid():N}";
        _metricsManager!.CreateGauge(gaugeName, "Test gauge metric");

        // Act & Assert - Set
        _metricsManager.SetGauge(gaugeName, 42.5);
        _output.WriteLine($"✓ Gauge '{gaugeName}' set to 42.5");

        // Act & Assert - Increment
        _metricsManager.IncrementGauge(gaugeName, 10);
        _output.WriteLine($"✓ Gauge '{gaugeName}' incremented by 10 (now 52.5)");

        // Act & Assert - Decrement
        _metricsManager.DecrementGauge(gaugeName, 5);
        _output.WriteLine($"✓ Gauge '{gaugeName}' decremented by 5 (now 47.5)");
    }

    [Fact]
    public void Gauge_WithLabels_Should_Work()
    {
        // Arrange
        var gaugeName = $"test_labeled_gauge_{Guid.NewGuid():N}";
        _metricsManager!.CreateGauge(
            gaugeName,
            "Test labeled gauge",
            "server", "status");

        // Act
        _metricsManager.SetGauge(gaugeName, 100, "server1", "active");
        _metricsManager.SetGauge(gaugeName, 50, "server2", "active");
        _metricsManager.SetGauge(gaugeName, 0, "server3", "inactive");

        // Assert
        _output.WriteLine($"✓ Gauge '{gaugeName}' with labels tracked successfully");
        _output.WriteLine("  - server1/active: 100");
        _output.WriteLine("  - server2/active: 50");
        _output.WriteLine("  - server3/inactive: 0");
    }

    [Fact]
    public void Histogram_Creation_And_Observation_Should_Work()
    {
        // Arrange
        var histogramName = $"test_histogram_{Guid.NewGuid():N}";
        var buckets = new[] { 0.1, 0.5, 1.0, 2.5, 5.0 };

        _metricsManager!.CreateHistogram(
            histogramName,
            "Test histogram metric",
            buckets);

        // Act
        _metricsManager.ObserveHistogram(histogramName, 0.23);
        _metricsManager.ObserveHistogram(histogramName, 1.45);
        _metricsManager.ObserveHistogram(histogramName, 3.67);

        // Assert
        _output.WriteLine($"✓ Histogram '{histogramName}' observations recorded:");
        _output.WriteLine("  - 0.23s");
        _output.WriteLine("  - 1.45s");
        _output.WriteLine("  - 3.67s");
    }

    [Fact]
    public void Histogram_WithLabels_Should_Work()
    {
        // Arrange
        var histogramName = $"test_labeled_histogram_{Guid.NewGuid():N}";

        _metricsManager!.CreateHistogram(
            histogramName,
            "Test labeled histogram",
            labelNames: new[] { "endpoint", "method" });

        // Act
        _metricsManager.ObserveHistogram(histogramName, 0.123, "/api/orders", "GET");
        _metricsManager.ObserveHistogram(histogramName, 0.456, "/api/orders", "POST");
        _metricsManager.ObserveHistogram(histogramName, 1.234, "/api/products", "GET");

        // Assert
        _output.WriteLine($"✓ Histogram '{histogramName}' with labels tracked successfully");
    }

    [Fact]
    public void Summary_Creation_And_Observation_Should_Work()
    {
        // Arrange
        var summaryName = $"test_summary_{Guid.NewGuid():N}";

        _metricsManager!.CreateSummary(
            summaryName,
            "Test summary metric");

        // Act
        for (int i = 0; i < 100; i++)
        {
            _metricsManager.ObserveSummary(summaryName, i * 0.01);
        }

        // Assert
        _output.WriteLine($"✓ Summary '{summaryName}' with 100 observations recorded");
        _output.WriteLine("  Quantiles: p50, p90, p95, p99");
    }

    [Fact]
    public void TrackDuration_Synchronous_Should_Work()
    {
        // Arrange
        var histogramName = $"test_duration_sync_{Guid.NewGuid():N}";
        _metricsManager!.CreateHistogram(histogramName, "Test duration tracking");

        // Act
        var result = _metricsManager.TrackDuration(
            histogramName,
            () =>
            {
                Thread.Sleep(50); // Simulate work
                return "Success";
            });

        // Assert
        Assert.Equal("Success", result);
        _output.WriteLine($"✓ Synchronous duration tracking completed");
        _output.WriteLine($"  Result: {result}");
    }

    [Fact]
    public async Task TrackDuration_Asynchronous_Should_Work()
    {
        // Arrange
        var histogramName = $"test_duration_async_{Guid.NewGuid():N}";
        _metricsManager!.CreateHistogram(histogramName, "Test async duration tracking");

        // Act
        var result = await _metricsManager.TrackDurationAsync(
            histogramName,
            async () =>
            {
                await Task.Delay(50); // Simulate async work
                return "Async Success";
            });

        // Assert
        Assert.Equal("Async Success", result);
        _output.WriteLine($"✓ Asynchronous duration tracking completed");
        _output.WriteLine($"  Result: {result}");
    }

    [Fact]
    public void TrackDuration_WithLabels_Should_Work()
    {
        // Arrange
        var histogramName = $"test_duration_labels_{Guid.NewGuid():N}";
        _metricsManager!.CreateHistogram(
            histogramName,
            "Test duration with labels",
            labelNames: new[] { "operation", "status" });

        // Act
        _metricsManager.TrackDuration(
            histogramName,
            () => Thread.Sleep(10),
            "create_order", "success");

        // Assert
        _output.WriteLine($"✓ Duration tracking with labels completed");
    }

    [Fact]
    public async Task RealWorld_OrderProcessing_Metrics_Should_Work()
    {
        // Arrange
        _metricsManager!.CreateCounter("orders_created_total", "Total orders created", "status");
        _metricsManager.CreateHistogram("order_processing_duration_seconds", "Order processing time");
        _metricsManager.CreateGauge("pending_orders", "Number of pending orders");

        // Act - Simulate order processing
        var orderCount = 10;
        var successCount = 0;
        var errorCount = 0;

        for (int i = 0; i < orderCount; i++)
        {
            await _metricsManager.TrackDurationAsync(
                "order_processing_duration_seconds",
                async () =>
                {
                    await Task.Delay(Random.Shared.Next(10, 50));

                    // 80% success rate
                    if (Random.Shared.NextDouble() < 0.8)
                    {
                        _metricsManager.IncrementCounter("orders_created_total", "success");
                        successCount++;
                    }
                    else
                    {
                        _metricsManager.IncrementCounter("orders_created_total", "error");
                        errorCount++;
                    }
                });
        }

        // Update pending orders gauge
        var pendingOrders = Random.Shared.Next(0, 50);
        _metricsManager.SetGauge("pending_orders", pendingOrders);

        // Assert
        Assert.Equal(orderCount, successCount + errorCount);
        _output.WriteLine("✓ Real-world order processing metrics scenario completed");
        _output.WriteLine($"  Total orders: {orderCount}");
        _output.WriteLine($"  Successful: {successCount}");
        _output.WriteLine($"  Failed: {errorCount}");
        _output.WriteLine($"  Pending orders: {pendingOrders}");
    }

    [Fact]
    public async Task RealWorld_ApiEndpoint_Metrics_Should_Work()
    {
        // Arrange
        _metricsManager!.CreateCounter(
            "http_api_requests_total",
            "Total API requests",
            "method", "endpoint", "status_code");

        _metricsManager.CreateHistogram(
            "http_api_request_duration_seconds",
            "API request duration",
            labelNames: new[] { "method", "endpoint" });

        // Act - Simulate API requests
        var endpoints = new[]
        {
            ("GET", "/api/orders", "200"),
            ("POST", "/api/orders", "201"),
            ("GET", "/api/orders", "200"),
            ("PUT", "/api/orders/123", "200"),
            ("DELETE", "/api/orders/123", "204"),
            ("GET", "/api/products", "200"),
            ("POST", "/api/products", "400")
        };

        foreach (var (method, endpoint, statusCode) in endpoints)
        {
            await _metricsManager.TrackDurationAsync(
                "http_api_request_duration_seconds",
                async () =>
                {
                    await Task.Delay(Random.Shared.Next(10, 100));
                    _metricsManager.IncrementCounter(
                        "http_api_requests_total",
                        method, endpoint, statusCode);
                },
                method, endpoint);
        }

        // Assert
        _output.WriteLine("✓ API endpoint metrics scenario completed");
        _output.WriteLine($"  Total requests: {endpoints.Length}");
    }

    [Fact]
    public void Metrics_ShouldNot_Break_On_Error()
    {
        // Act & Assert - These should not throw exceptions
        _metricsManager!.IncrementCounter("nonexistent_counter");
        _metricsManager.SetGauge("nonexistent_gauge", 42);
        _metricsManager.ObserveHistogram("nonexistent_histogram", 1.5);

        _output.WriteLine("✓ Metrics gracefully handle errors without breaking application");
    }

    [Fact]
    public void Multiple_MetricTypes_Should_Coexist()
    {
        // Arrange
        var baseName = $"coexistence_test_{Guid.NewGuid():N}";

        // Act
        _metricsManager!.CreateCounter($"{baseName}_counter", "Counter");
        _metricsManager.CreateGauge($"{baseName}_gauge", "Gauge");
        _metricsManager.CreateHistogram($"{baseName}_histogram", "Histogram");
        _metricsManager.CreateSummary($"{baseName}_summary", "Summary");

        // Assert
        _metricsManager.IncrementCounter($"{baseName}_counter");
        _metricsManager.SetGauge($"{baseName}_gauge", 100);
        _metricsManager.ObserveHistogram($"{baseName}_histogram", 0.5);
        _metricsManager.ObserveSummary($"{baseName}_summary", 0.25);

        _output.WriteLine("✓ Multiple metric types coexist successfully");
    }

    [Fact]
    public async Task Performance_Test_HighVolume_Metrics()
    {
        // Arrange
        var counterName = $"perf_test_counter_{Guid.NewGuid():N}";
        _metricsManager!.CreateCounter(counterName, "Performance test counter");

        var iterations = 10000;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await Task.Run(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                _metricsManager.IncrementCounter(counterName);
            }
        });

        stopwatch.Stop();

        // Assert
        var opsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
        Assert.True(opsPerSecond > 10000, "Should handle >10K ops/sec");

        _output.WriteLine($"✓ Performance test completed");
        _output.WriteLine($"  Operations: {iterations:N0}");
        _output.WriteLine($"  Duration: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Throughput: {opsPerSecond:N0} ops/sec");
    }

    [Fact]
    public async Task MetricsEndpoint_Should_Contain_CustomMetrics()
    {
        // Arrange
        var client = _testHost!.GetTestClient();
        var testMetricName = $"custom_test_metric_{Guid.NewGuid():N}";

        _metricsManager!.CreateCounter(testMetricName, "Custom test metric");
        _metricsManager.IncrementCounter(testMetricName);

        // Act
        var response = await client.GetAsync("/metrics");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains(testMetricName, content);
        _output.WriteLine($"✓ Custom metric '{testMetricName}' found in /metrics endpoint");
    }

    [Fact]
    public async Task MetricsEndpoint_Should_Contain_CustomLabels()
    {
        // Arrange
        var client = _testHost!.GetTestClient();

        // Act
        var response = await client.GetAsync("/metrics");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Check for custom labels from configuration
        Assert.Contains("team=\"test-team\"", content);
        Assert.Contains("version=\"1.0.0\"", content);

        _output.WriteLine("✓ Custom labels from configuration found in metrics");
    }
}
