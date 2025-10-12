using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace Custom.Framework.Monitoring.Grafana;

/// <summary>
/// Extension methods for registering Grafana services
/// </summary>
public static class GrafanaExtensions
{
    /// <summary>
    /// Add Grafana integration to the service collection
    /// </summary>
    public static IServiceCollection AddGrafana(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<GrafanaOptions>(
            configuration.GetSection(GrafanaOptions.ConfigSectionName));

        var options = configuration
            .GetSection(GrafanaOptions.ConfigSectionName)
            .Get<GrafanaOptions>() ?? new GrafanaOptions();

        if (!options.Enabled)
        {
            return services;
        }

        // Register HttpClient with retry policy
        services.AddHttpClient<IGrafanaClient, GrafanaClient>()
            .AddPolicyHandler(GetRetryPolicy(options))
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        // Register annotation service if enabled
        if (options.Annotations.EnableDeploymentAnnotations)
        {
            services.AddSingleton<IGrafanaAnnotationService, GrafanaAnnotationService>();
        }

        // Register dashboard provisioning service if enabled
        if (options.AutoProvisionDashboards || options.AutoProvisionDataSources)
        {
            services.AddHostedService<GrafanaProvisioningService>();
        }

        return services;
    }

    /// <summary>
    /// Add Grafana with custom configuration
    /// </summary>
    public static IServiceCollection AddGrafana(
        this IServiceCollection services,
        Action<GrafanaOptions> configureOptions)
    {
        services.Configure(configureOptions);
        
        var options = new GrafanaOptions();
        configureOptions(options);

        if (!options.Enabled)
        {
            return services;
        }

        services.AddHttpClient<IGrafanaClient, GrafanaClient>()
            .AddPolicyHandler(GetRetryPolicy(options))
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        if (options.Annotations.EnableDeploymentAnnotations)
        {
            services.AddSingleton<IGrafanaAnnotationService, GrafanaAnnotationService>();
        }

        if (options.AutoProvisionDashboards || options.AutoProvisionDataSources)
        {
            services.AddHostedService<GrafanaProvisioningService>();
        }

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(GrafanaOptions options)
    {
        if (!options.EnableRetry)
        {
            return Policy.NoOpAsync<HttpResponseMessage>();
        }

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                options.MaxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }
}
