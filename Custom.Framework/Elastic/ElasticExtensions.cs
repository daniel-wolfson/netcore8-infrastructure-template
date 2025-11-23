using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.Elasticsearch;
using Serilog.Sinks.File;
using System.Reflection;

namespace Custom.Framework.Elastic;

/// <summary>
/// Extension methods for configuring Elasticsearch integration
/// </summary>
public static class ElasticExtensions
{
    /// <summary>
    /// Adds Elasticsearch services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddElasticsearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ElasticOptions>(
            configuration.GetSection("Elasticsearch"));

        services.AddSingleton<IElasticClientFactory, ElasticClientFactory>();

        // Register health check
        var options = configuration
            .GetSection("Elasticsearch")
            .Get<ElasticOptions>();

        if (options?.EnableHealthCheck == true)
        {
            services.AddHealthChecks()
                .AddCheck<ElasticsearchHealthCheck>("Elasticsearch");
        }

        // Register metrics
        if (options?.EnableMetrics == true)
        {
            services.AddSingleton<ElasticsearchMetrics>();
        }

        return services;
    }

    /// <summary>
    /// Adds Elasticsearch sink to Serilog logger configuration
    /// </summary>
    public static LoggerConfiguration AddElasticsearchSink(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection("Elasticsearch")
            .Get<ElasticOptions>();

        if (options == null || options.Nodes.Length == 0)
        {
            Log.Warning("Elasticsearch configuration not found. Skipping Elasticsearch sink.");
            return loggerConfiguration;
        }

        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
        var serviceName = configuration["ServiceName"] ?? 
            Assembly.GetEntryAssembly()?.GetName().Name ?? "Unknown";

        var indexFormat = options.IndexFormat
            .Replace("{environment}", environment.ToLowerInvariant())
            .Replace("{serviceName}", serviceName.ToLowerInvariant());

        return loggerConfiguration.WriteTo.Elasticsearch(
            new ElasticsearchSinkOptions(options.Nodes.Select(n => new Uri(n)))
            {
                IndexFormat = indexFormat,
                AutoRegisterTemplate = options.AutoRegisterTemplate,
                AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv8,
                NumberOfShards = options.NumberOfShards,
                NumberOfReplicas = options.NumberOfReplicas,
                BufferBaseFilename = options.DeadLetterQueuePath,
                BufferFileCountLimit = 31,
                BufferLogShippingInterval = TimeSpan.FromSeconds(5),
                MinimumLogEventLevel = Enum.Parse<LogEventLevel>(options.MinimumLogLevel),
                ModifyConnectionSettings = conn =>
                {
                    if (!string.IsNullOrEmpty(options.ApiKey))
                    {
                        conn.ApiKeyAuthentication(
                            new Elasticsearch.Net.ApiKeyAuthenticationCredentials(options.ApiKey));
                    }
                    else if (!string.IsNullOrEmpty(options.Username))
                    {
                        conn.BasicAuthentication(options.Username, options.Password);
                    }

                    conn.RequestTimeout(TimeSpan.FromSeconds(options.RequestTimeout))
                        .MaximumRetries(options.MaxRetries)
                        .EnableHttpCompression(options.EnableCompression);

                    return conn;
                },
                EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog |
                                   (options.EnableDeadLetterQueue
                                       ? EmitEventFailureHandling.WriteToFailureSink
                                       : EmitEventFailureHandling.ThrowException),
                FailureSink = options.EnableDeadLetterQueue
                    ? new FileSink(
                        Path.Combine(
                            options.DeadLetterQueuePath ?? "./logs/dlq",
                            "elastic-dlq-.txt"),
                        new JsonFormatter(),
                        null)
                    : null
            });
    }

    /// <summary>
    /// Enriches log events with Elasticsearch-specific context
    /// </summary>
    public static LoggerConfiguration EnrichWithElasticsearchContext(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration)
    {
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
        var serviceName = configuration["ServiceName"] ?? 
            Assembly.GetEntryAssembly()?.GetName().Name ?? "Unknown";
        var version = configuration["Version"] ?? "1.0.0";

        return loggerConfiguration
            .Enrich.WithProperty("Environment", environment)
            .Enrich.WithProperty("ServiceName", serviceName)
            .Enrich.WithProperty("ServiceVersion", version)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .Enrich.WithEnvironmentUserName();
    }
}
