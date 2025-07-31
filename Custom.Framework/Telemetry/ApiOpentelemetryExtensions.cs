using Azure.Monitor.OpenTelemetry.Exporter;
using Custom.Framework.Extensions;
using Custom.Framework.Helpers;
using Custom.Framework.Telemetry.Isrotel.Framework.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Reflection;

namespace Custom.Framework.Telemetry
{
    public static class ApiOpentelemetryExtensions
    {
        /// <summary>
        /// ConfigureOpenTelemetry  - configure OpenTelemetry for the API.
        /// </summary>
        public static IServiceCollection ConfigureOpenTelemetry(this IServiceCollection services, IConfiguration config)
        {
            var version = config["Version"] ?? "v";
            var environment = config["ASPNETCORE_ENVIRONMENT"];

            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(
                    serviceName: ApiHelper.ServiceName,
                    serviceVersion: Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
                    autoGenerateServiceInstanceId: false,
                    serviceInstanceId: ApiHelper.ServiceName
                )
                .AddAttributes(new KeyValuePair<string, object>[]
                {
                    new("version", version),
                    new("environment", environment ?? "unknown")
                });

            var openTelemetryBuilder = services.AddOpenTelemetry();

            openTelemetryBuilder
                .WithMetrics(metrics =>
                {
                    if (environment?.Equals("development", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        //metrics.AddMeter("Microsoft.AspNetCore.Hosting")
                        //metrics.AddMeter("Microsoft.AspNetCore.Diagnostics")
                        metrics.SetResourceBuilder(resourceBuilder);
                        metrics.AddMeter(ApiHelper.ServiceName);

                        // Debugging purposes
                        metrics.AddConsoleExporter();
                        metrics.AddOtlpExporter(options =>
                         {
                             var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                                    ?? config["OpenTelemetryConfig:MetricsCollectorUrl"];

                             if (otlpEndpoint is not null)
                             {
                                 options.Endpoint = new Uri(otlpEndpoint);
                             }
                         });
                    }
                })
                .WithTracing(tracerProviderBuilder =>
                {
                    tracerProviderBuilder
                        .SetSampler(new ConditionalSampler())
                        .SetResourceBuilder(resourceBuilder)
                        .AddSource(ApiHelper.ServiceName)
                        .AddHttpClientInstrumentation(options =>
                        {
                            options.FilterHttpRequestMessage = (httpRequestMessage) =>
                            {
                                var isDebugMode = httpRequestMessage.RequestUri?
                                    .Query.Contains("IsDebugMode=true", StringComparison.CurrentCultureIgnoreCase) ?? false;
                                return isDebugMode;
                            };
                        })
                        .AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                                ?? config["OpenTelemetryConfig:TracesCollectorUrl"]!);
                        })
                        .AddAspNetCoreInstrumentation(options =>
                        {
                            options.EnrichWithHttpRequest = (activity, httpRequest) =>
                            {
                                activity.SetTag("http.request.method", httpRequest.Method);
                                activity.SetTag("http.request.path", httpRequest.Path);
                            };
                            options.Filter = (httpRequestMessage) =>
                            {
                                var isDebugMode = httpRequestMessage.Request.HttpContext.IsRequestDebugMode();
                                return !isDebugMode;
                            };
                            options.RecordException = true;
                        });

                    if (environment?.Equals("development", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        tracerProviderBuilder.AddConsoleExporter();

                        // Optional: Uncomment if using Jaeger locally
                        // builder.AddJaegerExporter(options =>
                        // {
                        //     options.AgentHost = "localhost";
                        //     options.AgentPort = 6831;
                        // });
                    }
                    else
                    {
                        tracerProviderBuilder.AddAzureMonitorTraceExporter(options =>
                        {
                            options.ConnectionString = config["ApplicationInsights:ConnectionString"];
                        });
                    }
                });

            //services.AddSingleton<IOpenTelemetryBuilder>(openTelemetryBuilder);
            //services.AddApplicationInsightsTelemetryProcessor<ExcludeDotnetMetricsProcessor>();


            // Optional: Use Azure Monitor for metrics and logging
            //openTelemetryBuilder.UseAzureMonitor(o =>
            //{
            //    var appInsightsConnectionString = config["ApplicationInsights:ConnectionString"];
            //    o.ConnectionString = appInsightsConnectionString;
            //    o.Credential = new DefaultAzureCredential();
            //});

            // Scoped activity source for APIs
            services.AddScoped<IApiActivityFactory, ApiActivityFactory>();

            return services;
        }

        private static void ConfigureExporters(TracerProviderBuilder builder, IConfiguration config, string? environment)
        {
            if (environment?.Equals("development", StringComparison.OrdinalIgnoreCase) == true)
            {
                builder.AddConsoleExporter();

                // Optional: Uncomment if using Jaeger locally
                // builder.AddJaegerExporter(options =>
                // {
                //     options.AgentHost = "localhost";
                //     options.AgentPort = 6831;
                // });
            }
            else
            {
                builder.AddAzureMonitorTraceExporter(options =>
                {
                    options.ConnectionString = config["ApplicationInsights:ConnectionString"];
                });
            }

            //builder.AddOtlpExporter(); // For OpenTelemetry Protocol
        }

        /// <summary> 
        /// Replaces the first section of the GUID with the specified value.
        /// </summary>
        public static string UpdateSection(this Guid guid, string newFirstSection)
        {
            if (string.IsNullOrEmpty(newFirstSection) || newFirstSection.Length != 8)
                newFirstSection = newFirstSection.PadRight(8, '0');

            // Convert the GUID to a string and split by '-'
            string guidString = guid.ToString();
            string[] sections = guidString.Split('-');

            // Replace the first section with the specified value
            sections[0] = newFirstSection;

            // Combine the sections back into a GUID format string
            return string.Join("-", sections);
        }

        // TODO: Temp, No Delete!
        public static IServiceCollection ConfigureOpenTelemetry_temp(this IServiceCollection services, IConfiguration config)
        {
            //TODO?: Directly logging  to telemetry client
            //builder.Logging.AddOpenTelemetry(logging =>
            //{
            //    logging.IncludeFormattedMessage = true;
            //    logging.IncludeScopes = true;
            //});
            //builder.Services.AddLogging((loggingBuilder) =>
            //{
            //    var openTelemetryConfig = builder.Config
            //        .GetSection(OpenTelemetryConfig.ConfigSectionName)
            //        .Get<OpenTelemetryConfig>()!;
            //    
            //    loggingBuilder.AddOpenTelemetry(o =>
            //    {
            //        o.SetResourceBuilder(resourceBuilder)
            //        .AddOtlpExporter()
            //        .AddOtlpExporter(options =>
            //        {
            //            options.Endpoint = new Uri(openTelemetryConfig.MetricsCollectorUrl);
            //            //opts.Endpoint = new Uri(loggingBuilder.Config["Otlp:Endpoint"]);
            //            options.ExportProcessorType = ExportProcessorType.Batch;
            //            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
            //        });
            //    });
            //});

            // Add Application Insights telemetry (potentialy to delete)
            //builder.Services.AddApplicationInsightsTelemetry(builder.Configuration["ApplicationInsights:InstrumentationKey"]);
            //builder.Services.AddSingleton<ITelemetryInitializer, ApiCustomTelemetryInitializer>();
            //builder.Services.AddSingleton<ITelemetryProcessor, ApiCustomTelemetryProcessor>();

            //var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Config["OTEL_EXPORTER_OTLP_ENDPOINT"]);
            //if (useOtlpExporter)
            //{
            //    builder.Services.Configure<OpenTelemetryLoggerOptions>(logging => logging.AddOtlpExporter());
            //    builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter());
            //    builder.Services.ConfigureOpenTelemetryTracerProvider(tracerProviderBuilder => tracerProviderBuilder.AddOtlpExporter());
            //}
            //builder.Services.AddSingleton(TracerProvider.Default.GetTracer(builder.Environment.ApplicationName));

            //services.AddApplicationInsightsTelemetry(options =>
            //    options.ConnectionString = configuration["ApplicationInsights:ConnectionString"]);

            //services.AddScoped(sp =>
            // {
            //     var connectionString = config["ApplicationInsights:ConnectionString"]?.ToString();
            //     var telemetryConfiguration = TelemetryConfiguration.CreateDefault();
            //     if (telemetryConfiguration != null) telemetryConfiguration.ConnectionString = connectionString;
            //     var client = new TelemetryClient(telemetryConfiguration);
            //     return client;
            // });

            return services;
        }
    }
}
