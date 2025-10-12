using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Prometheus;
using Prometheus.DotNetRuntime;

namespace Custom.Framework.Monitoring.Prometheus;

/// <summary>
/// Extension methods for configuring Prometheus in ASP.NET Core applications
/// </summary>
public static class PrometheusExtensions
{
    /// <summary>
    /// Add Prometheus metrics to the service collection
    /// </summary>
    public static IServiceCollection AddPrometheusMetrics(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options from appsettings.json
        services.Configure<PrometheusOptions>(
            configuration.GetSection(PrometheusOptions.ConfigSectionName));

        var options = configuration
            .GetSection(PrometheusOptions.ConfigSectionName)
            .Get<PrometheusOptions>() ?? new PrometheusOptions();

        if (!options.Enabled)
        {
            return services;
        }

        // Register the metrics manager
        services.AddSingleton<IPrometheusMetricsService, PrometheusMetricsService>();

        // Enable .NET runtime metrics (GC, JIT, ThreadPool, etc.)
        if (options.EnableRuntimeMetrics)
        {
            try
            {
                IDisposable? collector = DotNetRuntimeStatsBuilder
                    .Customize()
                    .WithContentionStats()
                    .WithGcStats()
                    .WithJitStats()
                    .WithThreadPoolStats()
                    .WithExceptionStats()
                    .StartCollecting();

                services.AddSingleton(collector);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not enable .NET runtime metrics: {ex.Message}");
            }
        }

        // Note: System metrics (CPU, memory, disk) are handled by Prometheus node_exporter in production
        // For process-level metrics, use EnableRuntimeMetrics

        // Configure Pushgateway if enabled
        if (options.Pushgateway?.Enabled == true)
        {
            services.AddHostedService<PromethehusPushgatewayService>();
        }

        return services;
    }

    /// <summary>
    /// Add Prometheus metrics with custom configuration
    /// </summary>
    public static IServiceCollection AddPrometheusMetrics(
        this IServiceCollection services,
        Action<PrometheusOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IPrometheusMetricsService, PrometheusMetricsService>();
        return services;
    }

    /// <summary>
    /// Use Prometheus metrics endpoint middleware
    /// This exposes the /metrics endpoint for Prometheus scraping
    /// </summary>
    public static IApplicationBuilder UsePrometheusMetrics(
        this IApplicationBuilder app)
    {
        var options = app.ApplicationServices
            .GetService<IOptions<PrometheusOptions>>()?.Value
            ?? new PrometheusOptions();

        if (!options.Enabled)
        {
            return app;
        }

        // Add HTTP metrics middleware (tracks all HTTP requests)
        if (options.EnableAspNetCoreMetrics)
        {
            app.UseHttpMetrics(configure =>
            {
                // Customize which routes to track
                configure.ReduceStatusCodeCardinality();
            });
        }

        // Expose /metrics endpoint
        app.UseMetricServer(options.MetricsEndpoint);

        return app;
    }

    /// <summary>
    /// Use Prometheus metrics with routing (for endpoint routing)
    /// </summary>
    public static IEndpointRouteBuilder MapPrometheusMetrics(
        this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider
            .GetService<IOptions<PrometheusOptions>>()?.Value
            ?? new PrometheusOptions();

        if (options.Enabled)
        {
            // Map the metrics endpoint
            endpoints.MapMetrics(options.MetricsEndpoint);
        }

        return endpoints;
    }

    /// <summary>
    /// Add database metrics for Entity Framework Core
    /// </summary>
    public static IServiceCollection AddPrometheusEFCoreMetrics(
        this IServiceCollection services)
    {
        // This will be called by DbContext interceptors
        // Implementation depends on EF Core setup
        return services;
    }

    /// <summary>
    /// Add HTTP client metrics for outgoing HTTP calls
    /// </summary>
    public static IHttpClientBuilder AddPrometheusHttpMetrics(
        this IHttpClientBuilder builder)
    {
        builder.UseHttpClientMetrics();
        return builder;
    }
}
