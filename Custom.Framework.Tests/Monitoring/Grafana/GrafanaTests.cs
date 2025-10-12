using Custom.Framework.Monitoring.Grafana;
using Custom.Framework.Monitoring.Grafana.Models;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Monitoring.Grafana;

/// <summary>
/// Integration tests for Grafana integration
/// Tests all fundamental actions for using Grafana monitoring
/// NOTE: These tests require a running Grafana instance
/// Run with: docker run -d -p 3000:3000 -e "GF_SECURITY_ADMIN_PASSWORD=admin" grafana/grafana:latest
/// </summary>
public class GrafanaTests(ITestOutputHelper output) : IAsyncLifetime
{
    private IHost? _testHost;
    private bool _grafanaAvailable;
    private IGrafanaClient? _grafanaClient;
    private IGrafanaAnnotationService? _annotationService;
    private readonly ITestOutputHelper _output = output;

    // Test configuration - override these via environment variables if needed
    private const string GrafanaUrl = "http://localhost:3001";
    private const string GrafanaUsername = "admin";
    private const string GrafanaPassword = "admin";

    public async Task InitializeAsync()
    {
        // Setup test host with Grafana
        _testHost = new HostBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Grafana:Enabled"] = "true",
                    ["Grafana:Url"] = Environment.GetEnvironmentVariable("GRAFANA_URL") ?? GrafanaUrl,
                    ["Grafana:Username"] = Environment.GetEnvironmentVariable("GRAFANA_USERNAME") ?? GrafanaUsername,
                    ["Grafana:Password"] = Environment.GetEnvironmentVariable("GRAFANA_PASSWORD") ?? GrafanaPassword,
                    ["Grafana:OrganizationId"] = "1",
                    ["Grafana:AutoProvisionDashboards"] = "false", // Disable for tests
                    ["Grafana:AutoProvisionDataSources"] = "false",
                    ["Grafana:TimeoutSeconds"] = "10",
                    ["Grafana:EnableRetry"] = "true",
                    ["Grafana:MaxRetryAttempts"] = "2",
                    ["Grafana:Annotations:EnableDeploymentAnnotations"] = "true",
                    ["Grafana:Annotations:DefaultTags:0"] = "test",
                    ["Grafana:Annotations:DefaultTags:1"] = "framework"
                });
            })
            .ConfigureServices((context, services) =>
            {
                services.AddGrafana(context.Configuration);
                services.AddLogging();
            })
            .Build();

        await _testHost.StartAsync();

        _grafanaClient = _testHost.Services.GetRequiredService<IGrafanaClient>();
        _annotationService = _testHost.Services.GetService<IGrafanaAnnotationService>();

        // Check if Grafana is available
        _grafanaAvailable = await _grafanaClient.HealthCheckAsync();

        if (_grafanaAvailable)
        {
            _output.WriteLine("✓ Grafana is available and healthy");
        }
        else
        {
            _output.WriteLine("⚠ Grafana is not available. Some tests will be skipped.");
            _output.WriteLine($"  Make sure Grafana is running at: {GrafanaUrl}");
            _output.WriteLine("  Run: docker run -d --name grafana-test -p 3001:3000 -e \"GF_SECURITY_ADMIN_PASSWORD=admin\" grafana/grafana:latest");
        }
    }

    #region Service Registration Tests

    [Fact]
    public void GrafanaClient_Should_Be_Registered()
    {
        // Assert
        Assert.NotNull(_grafanaClient);
        _output.WriteLine("✓ IGrafanaClient successfully registered in DI");
    }

    [Fact]
    public void AnnotationService_Should_Be_Registered()
    {
        // Assert
        Assert.NotNull(_annotationService);
        _output.WriteLine("✓ IGrafanaAnnotationService successfully registered in DI");
    }

    #endregion

    #region Health Check Tests

    [Fact]
    public async Task HealthCheck_Should_Return_Status()
    {
        // Act
        var isHealthy = await _grafanaClient!.HealthCheckAsync();

        // Assert
        _output.WriteLine($"Grafana health status: {(isHealthy ? "Healthy ✓" : "Unhealthy ✗")}");

        if (_grafanaAvailable)
        {
            Assert.True(isHealthy);
        }
    }

    #endregion

    #region Data Source Management Tests

    [Fact]
    public async Task List_All_Annotations_Visually()
    {
        if (!_grafanaAvailable)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Get all annotations from last 7 days
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;

        var annotations = await _grafanaClient!.GetAnnotationsAsync(from, to);

        _output.WriteLine("╔═══════════════════════════════════════════════════════╗");
        _output.WriteLine($"║  Found {annotations.Count} annotation(s)");
        _output.WriteLine("╚═══════════════════════════════════════════════════════╝");
        _output.WriteLine("");

        foreach (var annotation in annotations.OrderByDescending(a => a.Time))
        {
            _output.WriteLine("┌─────────────────────────────────────────────────────┐");
            _output.WriteLine($"│ 📍 Time: {annotation.DateTime:yyyy-MM-dd HH:mm:ss} UTC");
            _output.WriteLine($"│ 💬 Text: {annotation.Text}");
            _output.WriteLine($"│ 🏷️  Tags: {string.Join(", ", annotation.Tags)}");

            if (annotation.Id > 0)
                _output.WriteLine($"│ 🔑 ID: {annotation.Id}");

            if (!string.IsNullOrEmpty(annotation.DashboardUid))
                _output.WriteLine($"│ 📊 Dashboard: {annotation.DashboardUid}");

            if (annotation.PanelId.HasValue)
                _output.WriteLine($"│ 📈 Panel: {annotation.PanelId}");

            _output.WriteLine("└─────────────────────────────────────────────────────┘");
            _output.WriteLine("");
        }
    }

    [Fact]
    public async Task DataSource_Create_Should_Work()
    {
        // Skip if Grafana not available
        if (!_grafanaAvailable)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Arrange
        var dataSourceName = $"test_prometheus_{Guid.NewGuid():N}";
        var dataSource = new GrafanaDataSource
        {
            Name = dataSourceName,
            Type = "prometheus",
            Url = "http://prometheus:9090",
            Access = "proxy",
            IsDefault = false,
            JsonData = new GrafanaDataSourceJsonData
            {
                HttpMethod = "POST",
                TimeInterval = "15s"
            }
        };

        try
        {
            // Act
            var response = await _grafanaClient!.CreateDataSourceAsync(dataSource);

            // Assert
            Assert.NotNull(response);
            Assert.NotEqual(0, response.Id);
            Assert.NotEmpty(response.Uid);
            Assert.Equal(dataSourceName, response.Name);

            _output.WriteLine($"✓ Data source created successfully");
            _output.WriteLine($"  Name: {response.Name}");
            _output.WriteLine($"  UID: {response.Uid}");
            _output.WriteLine($"  ID: {response.Id}");

            // Cleanup
            await _grafanaClient.DeleteDataSourceAsync(response.Uid);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task DataSource_Get_Should_Work()
    {
        // Skip if Grafana not available
        if (!_grafanaAvailable)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Arrange - Create a data source first
        var dataSourceName = $"test_get_ds_{Guid.NewGuid():N}";
        var createResponse = await _grafanaClient!.CreateDataSourceAsync(new GrafanaDataSource
        {
            Name = dataSourceName,
            Type = "prometheus",
            Url = "http://prometheus:9090",
            Access = "proxy"
        });

        try
        {
            // Act - Get by UID
            var dataSource = await _grafanaClient.GetDataSourceAsync(createResponse.Uid);

            // Assert
            Assert.NotNull(dataSource);
            Assert.Equal(dataSourceName, dataSource.Name);
            Assert.Equal("prometheus", dataSource.Type);

            _output.WriteLine($"✓ Data source retrieved successfully by UID");
            _output.WriteLine($"  Name: {dataSource.Name}");
            _output.WriteLine($"  Type: {dataSource.Type}");
            _output.WriteLine($"  URL: {dataSource.Url}");

            // Act - Get by name
            var dataSourceByName = await _grafanaClient.GetDataSourceByNameAsync(dataSourceName);

            // Assert
            Assert.NotNull(dataSourceByName);
            Assert.Equal(dataSourceName, dataSourceByName.Name);

            _output.WriteLine($"✓ Data source retrieved successfully by name");
        }
        finally
        {
            // Cleanup
            await _grafanaClient!.DeleteDataSourceAsync(createResponse.Uid);
        }
    }

    [Fact]
    public async Task DataSource_GetAll_Should_Work()
    {
        // Skip if Grafana not available
        if (!_grafanaAvailable)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Act
        var dataSources = await _grafanaClient!.GetDataSourcesAsync();

        // Assert
        Assert.NotNull(dataSources);
        _output.WriteLine($"✓ Retrieved {dataSources.Count} data source(s)");

        foreach (var ds in dataSources.Take(5))
        {
            _output.WriteLine($"  - {ds.Name} ({ds.Type})");
        }
    }

    [Fact]
    public async Task DataSource_Delete_Should_Work()
    {
        // Skip if Grafana not available
        if (!_grafanaAvailable)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Arrange - Create a data source
        var dataSourceName = $"test_delete_ds_{Guid.NewGuid():N}";
        var createResponse = await _grafanaClient!.CreateDataSourceAsync(new GrafanaDataSource
        {
            Name = dataSourceName,
            Type = "prometheus",
            Url = "http://prometheus:9090"
        });

        // Act
        var deleted = await _grafanaClient.DeleteDataSourceAsync(createResponse.Uid);

        // Assert
        Assert.True(deleted);
        _output.WriteLine($"✓ Data source deleted successfully");

        // Verify it's gone
        var dataSource = await _grafanaClient.GetDataSourceAsync(createResponse.Uid);
        Assert.Null(dataSource);
        _output.WriteLine($"✓ Verified data source no longer exists");
    }

    #endregion

    #region Dashboard Management Tests

    [Fact]
    public async Task Dashboard_Create_Should_Work()
    {
        // Skip if Grafana not available
        if (!_grafanaAvailable)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Arrange
        var dashboardTitle = $"Test Dashboard {Guid.NewGuid():N}";
        var dashboard = new GrafanaDashboard
        {
            Title = dashboardTitle,
            Tags = new List<string> { "test", "automated" },
            Timezone = "browser",
            Panels = new List<GrafanaPanel>
            {
                new GrafanaPanel
                {
                    Id = 1,
                    Title = "Test Panel",
                    Type = "graph",
                    GridPos = new GrafanaGridPos { X = 0, Y = 0, W = 12, H = 8 }
                }
            }
        };

        try
        {
            // Act
            var response = await _grafanaClient!.CreateOrUpdateDashboardAsync(dashboard);

            // Assert
            Assert.NotNull(response);
            Assert.NotEmpty(response.Uid);
            Assert.NotEmpty(response.Url);
            Assert.Equal("success", response.Status);

            _output.WriteLine($"✓ Dashboard created successfully");
            _output.WriteLine($"  Title: {dashboardTitle}");
            _output.WriteLine($"  UID: {response.Uid}");
            _output.WriteLine($"  URL: {response.Url}");

            // Cleanup
            await _grafanaClient.DeleteDashboardAsync(response.Uid);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task Dashboard_Get_Should_Work()
    {
        // Skip if Grafana not available
        if (!_grafanaAvailable)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Arrange - Create a dashboard first
        var dashboardTitle = $"Test Get Dashboard {Guid.NewGuid():N}";
        var createResponse = await _grafanaClient!.CreateOrUpdateDashboardAsync(new GrafanaDashboard
        {
            Title = dashboardTitle,
            Tags = new List<string> { "test" }
        });

        try
        {
            // Act
            var dashboard = await _grafanaClient.GetDashboardAsync(createResponse.Uid);

            // Assert
            Assert.NotNull(dashboard);
            Assert.Equal(dashboardTitle, dashboard.Title);
            Assert.Contains("test", dashboard.Tags);

            _output.WriteLine($"✓ Dashboard retrieved successfully");
            _output.WriteLine($"  Title: {dashboard.Title}");
            _output.WriteLine($"  Tags: {string.Join(", ", dashboard.Tags)}");
        }
        finally
        {
            // Cleanup
            await _grafanaClient!.DeleteDashboardAsync(createResponse.Uid);
        }
    }

    [Fact]
    public async Task Dashboard_Update_Should_Work()
    {
        // Skip if Grafana not available
        if (!_grafanaAvailable)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Arrange - Create initial dashboard
        var originalTitle = $"Original Title {Guid.NewGuid():N}";
        var createResponse = await _grafanaClient!.CreateOrUpdateDashboardAsync(new GrafanaDashboard
        {
            Title = originalTitle
        });

        try
        {
            // Act - Update the dashboard
            var updatedTitle = $"Updated Title {Guid.NewGuid():N}";
            var updateResponse = await _grafanaClient.CreateOrUpdateDashboardAsync(
                new GrafanaDashboard
                {
                    Uid = createResponse.Uid,
                    Title = updatedTitle
                },
                overwrite: true);

            // Assert
            Assert.Equal(createResponse.Uid, updateResponse.Uid);

            var dashboard = await _grafanaClient.GetDashboardAsync(updateResponse.Uid);
            Assert.NotNull(dashboard);
            Assert.Equal(updatedTitle, dashboard.Title);

            _output.WriteLine($"✓ Dashboard updated successfully");
            _output.WriteLine($"  Original: {originalTitle}");
            _output.WriteLine($"  Updated: {updatedTitle}");
        }
        finally
        {
            // Cleanup
            await _grafanaClient!.DeleteDashboardAsync(createResponse.Uid);
        }
    }

    [Fact]
    public async Task Dashboard_Delete_Should_Work()
    {
        // Skip if Grafana not available
        if (!_grafanaAvailable)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Arrange - Create a dashboard
        var dashboardTitle = $"Test Delete Dashboard {Guid.NewGuid():N}";
        var createResponse = await _grafanaClient!.CreateOrUpdateDashboardAsync(new GrafanaDashboard
        {
            Title = dashboardTitle
        });

        // Act
        var deleted = await _grafanaClient.DeleteDashboardAsync(createResponse.Uid);

        // Assert
        Assert.True(deleted);
        _output.WriteLine($"✓ Dashboard deleted successfully");

        // Verify it's gone
        var dashboard = await _grafanaClient.GetDashboardAsync(createResponse.Uid);
        Assert.Null(dashboard);
        _output.WriteLine($"✓ Verified dashboard no longer exists");
    }

    [Fact]
    public async Task Dashboard_WithPanels_Should_Work()
    {
        // Skip if Grafana not available
        if (!_grafanaAvailable)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Arrange
        var dashboard = new GrafanaDashboard
        {
            Title = $"Dashboard with Panels {Guid.NewGuid():N}",
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
                            Expr = "rate(http_requests_total[5m])",
                            LegendFormat = "{{method}}",
                            RefId = "A"
                        }
                    }
                },
                new GrafanaPanel
                {
                    Id = 2,
                    Title = "Error Rate",
                    Type = "graph",
                    GridPos = new GrafanaGridPos { X = 12, Y = 0, W = 12, H = 8 },
                    Targets = new List<GrafanaTarget>
                    {
                        new GrafanaTarget
                        {
                            Expr = "rate(http_errors_total[5m])",
                            LegendFormat = "errors",
                            RefId = "B"
                        }
                    }
                }
            }
        };

        try
        {
            // Act
            var response = await _grafanaClient!.CreateOrUpdateDashboardAsync(dashboard);

            // Assert
            Assert.NotNull(response);

            var retrievedDashboard = await _grafanaClient.GetDashboardAsync(response.Uid);
            Assert.NotNull(retrievedDashboard);
            Assert.Equal(2, retrievedDashboard.Panels.Count);

            _output.WriteLine($"✓ Dashboard with panels created successfully");
            _output.WriteLine($"  Panels: {retrievedDashboard.Panels.Count}");
            foreach (var panel in retrievedDashboard.Panels)
            {
                _output.WriteLine($"  - {panel.Title} ({panel.Type})");
            }

            // Cleanup
            await _grafanaClient.DeleteDashboardAsync(response.Uid);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Annotation Tests

    [Fact]
    public async Task Annotation_Create_Should_Work()
    {
        // Skip if Grafana not available
        if (!_grafanaAvailable)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Arrange
        var annotation = new GrafanaAnnotation
        {
            DateTime = DateTime.UtcNow,
            Text = $"Test annotation {Guid.NewGuid():N}",
            Tags = new List<string> { "test", "automated" }
        };

        try
        {
            // Act
            var response = await _grafanaClient!.CreateAnnotationAsync(annotation);

            // Assert
            Assert.NotNull(response);
            Assert.NotEqual(0, response.Id);

            _output.WriteLine($"✓ Annotation created successfully");
            _output.WriteLine($"  ID: {response.Id}");
            _output.WriteLine($"  Text: {annotation.Text}");
            _output.WriteLine($"  Tags: {string.Join(", ", annotation.Tags)}");

            // Cleanup
            await _grafanaClient.DeleteAnnotationAsync(response.Id);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task Annotation_GetByTimeRange_Should_Work()
    {
        // Skip if Grafana not available
        if (!_grafanaAvailable)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Arrange - Create an annotation
        var annotation = new GrafanaAnnotation
        {
            DateTime = DateTime.UtcNow,
            Text = $"Test range annotation {Guid.NewGuid():N}",
            Tags = new List<string> { "test" }
        };

        var createResponse = await _grafanaClient!.CreateAnnotationAsync(annotation);

        try
        {
            // Act - Get annotations from last hour
            var from = DateTime.UtcNow.AddHours(-1);
            var to = DateTime.UtcNow.AddMinutes(1);
            var annotations = await _grafanaClient.GetAnnotationsAsync(from, to);

            // Assert
            Assert.NotNull(annotations);
            Assert.True(annotations.Count > 0);

            _output.WriteLine($"✓ Retrieved {annotations.Count} annotation(s) in time range");
            _output.WriteLine($"  From: {from:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine($"  To: {to:yyyy-MM-dd HH:mm:ss}");
        }
        finally
        {
            // Cleanup
            await _grafanaClient!.DeleteAnnotationAsync(createResponse.Id);
        }
    }

    [Fact]
    public async Task Annotation_Delete_Should_Work()
    {
        // Skip if Grafana not available
        if (!_grafanaAvailable)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Arrange - Create an annotation
        var annotation = new GrafanaAnnotation
        {
            DateTime = DateTime.UtcNow,
            Text = $"Test delete annotation {Guid.NewGuid():N}",
            Tags = new List<string> { "test" }
        };

        var createResponse = await _grafanaClient!.CreateAnnotationAsync(annotation);

        // Act
        var deleted = await _grafanaClient.DeleteAnnotationAsync(createResponse.Id);

        // Assert
        Assert.True(deleted);
        _output.WriteLine($"✓ Annotation deleted successfully");
    }

    [Fact]
    public async Task AnnotationService_MarkDeployment_Should_Work()
    {
        // Skip if Grafana not available or annotation service not registered
        if (!_grafanaAvailable || _annotationService == null)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available or annotation service not registered");
            return;
        }

        // Act
        await _annotationService.MarkDeploymentAsync("1.2.3", "Test deployment from integration test");

        // Assert
        _output.WriteLine($"✓ Deployment annotation created via service");
        _output.WriteLine($"  Version: 1.2.3");
        _output.WriteLine($"  Check Grafana UI to verify annotation");
    }

    [Fact]
    public async Task AnnotationService_MarkError_Should_Work()
    {
        // Skip if Grafana not available or annotation service not registered
        if (!_grafanaAvailable || _annotationService == null)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available or annotation service not registered");
            return;
        }

        // Act
        await _annotationService.MarkErrorAsync(
            "Test error from integration test",
            "This is a test error annotation");

        // Assert
        _output.WriteLine($"✓ Error annotation created via service");
        _output.WriteLine($"  Check Grafana UI to verify annotation");
    }

    [Fact]
    public async Task AnnotationService_MarkCustomEvent_Should_Work()
    {
        // Skip if Grafana not available or annotation service not registered
        if (!_grafanaAvailable || _annotationService == null)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available or annotation service not registered");
            return;
        }

        // Act
        await _annotationService.MarkEventAsync(
            "Custom test event",
            new List<string> { "custom", "test", "event" });

        // Assert
        _output.WriteLine($"✓ Custom event annotation created via service");
        _output.WriteLine($"  Tags: custom, test, event");
        _output.WriteLine($"  Check Grafana UI to verify annotation");
    }

    #endregion

    #region Real-World Scenario Tests

    [Fact]
    public async Task RealWorld_DeploymentTracking_Should_Work()
    {
        // Skip if Grafana not available
        if (!_grafanaAvailable || _annotationService == null)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Simulate a deployment workflow
        var version = $"1.{DateTime.UtcNow:yyyyMMdd}.{Random.Shared.Next(1, 100)}";

        _output.WriteLine($"Simulating deployment of version {version}...");

        // Mark deployment start
        await _annotationService.MarkEventAsync(
            $"Deployment {version} started",
            new List<string> { "deployment", "start" });

        _output.WriteLine($"  ✓ Marked deployment start");

        // Simulate deployment work
        await Task.Delay(100);

        // Mark deployment complete
        await _annotationService.MarkDeploymentAsync(
            version,
            $"Successfully deployed at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

        _output.WriteLine($"  ✓ Marked deployment complete");
        _output.WriteLine($"✓ Real-world deployment tracking completed");
    }

    [Fact]
    public async Task RealWorld_MonitoringDashboard_Should_Work()
    {
        // Skip if Grafana not available
        if (!_grafanaAvailable)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Arrange - Create a comprehensive monitoring dashboard
        var dashboard = new GrafanaDashboard
        {
            Title = $"Application Monitoring {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
            Tags = new List<string> { "monitoring", "application", "test" },
            Refresh = "30s",
            Time = new GrafanaTimeRange
            {
                From = "now-6h",
                To = "now"
            },
            Panels = new List<GrafanaPanel>
            {
                // Request Rate Panel
                new GrafanaPanel
                {
                    Id = 1,
                    Title = "HTTP Request Rate",
                    Type = "graph",
                    GridPos = new GrafanaGridPos { X = 0, Y = 0, W = 12, H = 8 },
                    Targets = new List<GrafanaTarget>
                    {
                        new GrafanaTarget
                        {
                            Expr = "rate(http_requests_received_total[5m])",
                            LegendFormat = "{{method}} {{endpoint}}",
                            RefId = "A"
                        }
                    }
                },
                // Error Rate Panel
                new GrafanaPanel
                {
                    Id = 2,
                    Title = "Error Rate",
                    Type = "graph",
                    GridPos = new GrafanaGridPos { X = 12, Y = 0, W = 12, H = 8 },
                    Targets = new List<GrafanaTarget>
                    {
                        new GrafanaTarget
                        {
                            Expr = "rate(http_requests_received_total{status_code=~\"5..\"}[5m])",
                            LegendFormat = "errors",
                            RefId = "B"
                        }
                    }
                },
                // Response Time Panel
                new GrafanaPanel
                {
                    Id = 3,
                    Title = "Response Time (p95)",
                    Type = "graph",
                    GridPos = new GrafanaGridPos { X = 0, Y = 8, W = 12, H = 8 },
                    Targets = new List<GrafanaTarget>
                    {
                        new GrafanaTarget
                        {
                            Expr = "histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))",
                            LegendFormat = "p95",
                            RefId = "C"
                        }
                    }
                }
            }
        };

        try
        {
            // Act
            var response = await _grafanaClient!.CreateOrUpdateDashboardAsync(dashboard);

            // Assert
            Assert.NotNull(response);

            _output.WriteLine($"✓ Monitoring dashboard created successfully");
            _output.WriteLine($"  Title: {dashboard.Title}");
            _output.WriteLine($"  UID: {response.Uid}");
            _output.WriteLine($"  URL: {response.Url}");
            _output.WriteLine($"  Panels: {dashboard.Panels.Count}");

            foreach (var panel in dashboard.Panels)
            {
                _output.WriteLine($"    - {panel.Title}");
            }

            // Cleanup
            await _grafanaClient.DeleteDashboardAsync(response.Uid);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GrafanaClient_Should_Handle_NotFound_Gracefully()
    {
        // Skip if Grafana not available
        if (!_grafanaAvailable)
        {
            _output.WriteLine("⚠ Test skipped - Grafana not available");
            return;
        }

        // Act
        var nonExistentUid = "nonexistent-uid-12345";
        var dashboard = await _grafanaClient!.GetDashboardAsync(nonExistentUid);
        var dataSource = await _grafanaClient.GetDataSourceAsync(nonExistentUid);

        // Assert
        Assert.Null(dashboard);
        Assert.Null(dataSource);

        _output.WriteLine($"✓ Client handles non-existent resources gracefully");
        _output.WriteLine($"  Dashboard (not found): null");
        _output.WriteLine($"  Data source (not found): null");
    }

    [Fact]
    public async Task GrafanaClient_Should_Handle_Invalid_Url()
    {
        // Arrange - Create client with invalid URL
        var invalidHost = new HostBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Grafana:Enabled"] = "true",
                    ["Grafana:Url"] = "http://invalid-grafana-host:9999",
                    ["Grafana:Username"] = "admin",
                    ["Grafana:Password"] = "admin",
                    ["Grafana:TimeoutSeconds"] = "2"
                });
            })
            .ConfigureServices((context, services) =>
            {
                services.AddGrafana(context.Configuration);
            })
            .Build();

        await invalidHost.StartAsync();

        var invalidClient = invalidHost.Services.GetRequiredService<IGrafanaClient>();

        // Act
        var isHealthy = await invalidClient.HealthCheckAsync();

        // Assert
        Assert.False(isHealthy);
        _output.WriteLine($"✓ Client handles invalid URL gracefully");
        _output.WriteLine($"  Health check returned: false (as expected)");

        await invalidHost.StopAsync();
        invalidHost.Dispose();
    }

    #endregion

    public async Task DisposeAsync()
    {
        if (_testHost != null)
        {
            await _testHost.StopAsync();
            _testHost.Dispose();
        }
    }
}
