using Asp.Versioning;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Custom.Framework.Cache;
using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Dal;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Configuration.Nop;
using Custom.Framework.Configuration.Optima;
using Custom.Framework.Configuration.Umbraco;
using Custom.Framework.Contracts;
using Custom.Framework.Core;
using Custom.Framework.Exceptions;
using Custom.Framework.HealthChecks;
using Custom.Framework.Helpers;
using Custom.Framework.Logging;
using Custom.Framework.Models.Base;
using Custom.Framework.Telemetry;
using Custom.Framework.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Collections;
using System.Net.Http.Headers;
using System.Reflection;
using ConfigurationManager = Microsoft.Extensions.Configuration.ConfigurationManager;

namespace Custom.Framework.Extensions
{
    public static class ApiExtensions
    {
        /// <summary>
        /// Make Api Versioning that it will to pass to X-Api-Version header each httpClient
        /// </summary>
        public static IServiceCollection ConfigureApiVersion(this IServiceCollection services, IConfiguration config, IWebHostEnvironment? environment = null)
        {
            try
            {
                int serviceMajorVersion = 1;
                int serviceMinorVersion = 0;
                var env = config.GetValue<string>("Version")?.ToUpperInvariant() ?? "V???";
                var assembly = Assembly.GetEntryAssembly();
                var assemblyVersion = assembly?.GetName().Version;

                if (assemblyVersion != null)
                {
                    serviceMajorVersion = assemblyVersion.Major;
                    serviceMinorVersion = assemblyVersion.Minor;
                }

                var apiVersion = new ApiVersion(serviceMajorVersion, serviceMinorVersion, env);
                services.AddApiVersioning(options =>
                {
                    options.DefaultApiVersion = apiVersion;
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.ReportApiVersions = true;
                    options.ApiVersionReader = ApiVersionReader.Combine(
                        new UrlSegmentApiVersionReader(),
                        new HeaderApiVersionReader("X-Api-Version")
                    );
                });
            }
            catch (Exception ex)
            {
                Log.Error("{TITLE} error: {MSG}", ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message);
            }

            return services;
        }

        /// <summary>
        /// Configure environments by combobox("SolutionConfigration") of of vs2022
        /// </summary>
        public static IServiceCollection ConfigureEnvironments(this IServiceCollection services,
            IConfiguration config, IWebHostEnvironment? environment = null)
        {
            // EnvironmentName: get from app args of main program
            var argEnvironmentName = config.Get<WebApplicationOptions>()?.Args?
                    .FirstOrDefault(x => x.Contains("environment"))?.Split("=")?.LastOrDefault();

            if (!string.IsNullOrEmpty(argEnvironmentName))
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", argEnvironmentName);

            string environmentName =
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", EnvironmentVariableTarget.User)
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", EnvironmentVariableTarget.Process)
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? config.GetValue<string>("environment")
                ?? "Production";
            environmentName = Char.ToUpperInvariant(environmentName[0]) + environmentName.Substring(1);
            config["environment"] = environmentName;
            if (environment != null)
            {
                environment.EnvironmentName = environmentName ?? environment.EnvironmentName;
                services.AddSingleton<IHostEnvironment>(environment);
                services.AddSingleton<IWebHostEnvironment>(environment);
            }

            var cfg = (config as IConfigurationManager)
                ?? new ConfigurationBuilder().AddConfiguration(config);

            cfg
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            return services;
        }

        /// <summary>
        /// Configures mappings and validations for the API.
        /// </summary>
        public static IServiceCollection ConfigureMappingsAndValidations(this IServiceCollection services, IConfigurationManager config)
        {
            services.AddTransient<IOptimaPackagesListValidator, OptimaPackagesListValidator>();
            services.AddTransient<IOptimaPlanCodeValidator, OptimaPlanDataValidator>();
            return services;
        }

        /// <summary>
        /// Configures the API settings.
        /// </summary>
        public static IServiceCollection ConfigureSettings(this IServiceCollection services, IConfiguration config)
        {
            return services.ConfigureSettings<ApiSettings>(config);
        }

        /// <summary>
        /// ConfigureAppSettings
        /// </summary>
        public static IServiceCollection ConfigureSettings<TOptions>(this IServiceCollection services,
            IConfiguration config, Action<TOptions>? configureOptions = null)
            where TOptions : ApiSettings
        {
            if (typeof(TOptions) != typeof(ApiSettings))
            {
                services.Configure<TOptions>(settings =>
                {
                    config.ConfigureAppSettings(settings);
                    try
                    {
                        configureOptions?.Invoke(settings);
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error("{TITLE} error: {MESSAGE}", ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message);
                        throw new ApiException(ex);
                    }
                });
            }

            services.Configure((Action<ApiSettings>)(settings =>
            {
                config.ConfigureAppSettings(settings);
            }));

            services.AddSingleton(config);

            var cfg = config as IConfigurationManager;
            if (cfg != null)
                services.AddSingleton<IConfigurationManager>((ConfigurationManager)config);

            //services.AddControllers().AddJsonOptions(options =>
            //{
            //    //options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            //});

            return services;
        }

        /// <summary>
        /// Configure HttpClients - read from appsettings.json and set the base address, token, and timeout.
        /// </summary>
        public static IServiceCollection ConfigureHttpClients(this IServiceCollection services, IConfiguration config, string rootSectionName = "Endpoints")
        {
            services.AddHttpClient();
            var endpoints = config.GetSection(rootSectionName).GetChildren().ToList();
            endpoints.ForEach(item =>
            {
                services.Configure<EndpointOptions>(item.Key, item);
                services.AddHttpClient(item.Key, (serviceProvider, httpClient) =>
                {
                    var currentEndpoint = item.Get<EndpointOptions>();
                    string parentToken = string.Empty;
                    string parentHost = string.Empty;
                    string parentRootPath = string.Empty;
                    string contentType = currentEndpoint?.ContentType ?? "application/json";
                    int timeout = 120;

                    if (!string.IsNullOrEmpty(currentEndpoint?.BaseOn))
                    {
                        var endpointBase = config.GetSection($"{rootSectionName}:{currentEndpoint?.BaseOn}").Get<EndpointOptions>();
                        parentToken = endpointBase?.Token ?? "";
                        parentHost = endpointBase?.Host ?? "";
                        parentRootPath = endpointBase?.RootPath ?? "";
                        timeout = endpointBase?.Timeout ?? 120;
                        contentType = endpointBase?.ContentType ?? contentType;
                    }

                    var token = currentEndpoint?.Token ?? parentToken;
                    var host = currentEndpoint?.Host ?? parentHost;
                    var rootPath = currentEndpoint?.RootPath ?? parentRootPath;

                    httpClient.BaseAddress = new Uri($"{host}{rootPath}"!);
                    if (!string.IsNullOrEmpty(contentType))
                        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                    else
                        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    if (!string.IsNullOrEmpty(token))
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
                    }

                    var httpContext = serviceProvider.GetService<IHttpContextAccessor>()?.HttpContext;

                    var webHostEnvironment = serviceProvider.GetService<IWebHostEnvironment>();

                    if (webHostEnvironment?.EnvironmentName == "Test" ||
                        (httpContext?.Items.TryGetValue(HttpContextItemsKeys.IsDebugMode, out var requestData) == true
                        && requestData is bool value && value))
                    {
                        timeout = 360; // TODO: set in appsettings?
                    }

                    httpClient.Timeout = TimeSpan.FromSeconds(timeout);

                })
                .ConfigurePrimaryHttpMessageHandler(sp => sp.GetService<ApiHttpClientHandler>()!);
            });

            services.AddHeaderPropagation(options =>
            {
                // forward the RequestHeaderKeys.CorrelationId if present.
                options.Headers.Add(RequestHeaderKeys.CorrelationId);
            });

            services.AddSingleton<IApiHttpClientFactory, ApiHttpClientFactory>();
            services.AddTransient<ApiHttpClientHandler>();
            return services;
        }

        /// <summary>
        /// ConfigureHealthChecks - add health checks for the API.
        /// </summary>
        public static IServiceCollection ConfigureHealthChecks(this IServiceCollection services,
            IConfiguration config, Action<IHealthChecksBuilder>? configureOptions = null)
        {
            var healthChecks = services.AddHealthChecks();
            healthChecks
                // Add a default liveness check to ensure app is responsive
                .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"])
                .AddCheck<VersionHealthCheck>("Version", tags: ["Version"])
                .AddCheck<RedisApiHealthCheck>("Redis", tags: ["Redis"])
                .AddCheck<AzureBlobApiHealthCheck>("AzureBlob", tags: ["AzureBlob"]);

            configureOptions?.Invoke(healthChecks);

            return services;
        }

        /// <summary>
        /// ConfigureAutofac - configure Autofac for the API.
        /// </summary>
        public static IServiceCollection ConfigureAutofac(this IServiceCollection services, IConfiguration config)
        {
            var container = new ContainerBuilder();
            container.Populate(services);
            var serviceProvider = new AutofacServiceProvider(container.Build());
            services.AddSingleton<IServiceProvider>(serviceProvider);
            //loggingBuilder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
            return services;
        }

        /// <summary>
        /// ConfigureLogging - configure logging for the API.
        /// </summary>
        public static WebApplicationBuilder ConfigureLogging(this WebApplicationBuilder builder)
        {

            builder.Logging.ClearProviders();
            var loggerConfiguration = new LoggerConfiguration();
            if (builder.Environment.IsDevelopment())
            {
                loggerConfiguration.MinimumLevel.Debug();
                loggerConfiguration.WriteTo.Console();
            }

            Log.Logger = loggerConfiguration.CreateLogger();
            // We should add this line because someone added the Serilog.ILogger globally instead of MSDN ILogger<T>.
            // Until then don't delete the 2 lines below.
            builder.Services.AddSingleton(Log.Logger);
            builder.Logging.AddSerilog();

            return builder;

            // Todo: remove if not needed.
            //var levelSwitch = new LoggingLevelSwitch();

            //var loggerConfiguration = new LoggerConfiguration()
            //    .MinimumLevel.ControlledBy(levelSwitch)
            //    .ReadFrom.Configuration(config)
            //    .Enrich.FromLogContext();

            //var openTelemetryConfig = config
            //    .GetSection(OpenTelemetryConfig.ConfigSectionName)
            //    .Get<OpenTelemetryConfig>()!;

            //var logger = new LoggerConfiguration()
            //    .MinimumLevel.Information()
            //    .WriteTo.Console(theme: AnsiConsoleTheme.Code)
            //    .CreateLogger();

            //var kestrelConfig = config.GetSection("Kestrel");
            //var minimumLevel = config["Serilog:MinimumLevel"];

            //logger.Information($"ServiceName: {Assembly.GetEntryAssembly()?.GetName().Name}");
            //logger.Information($"Environment: {config["environment"]}");
            //logger.Information($"ASPNETCORE_ENVIRONMENT={environmentName}");
            //logger.Information($"Version: {config["Version"]}");
            //logger.Information($"Host: {config["Kestrel:Endpoints:Https:Url"] ?? config["Kestrel:Endpoints:Http:Url"]}");
            //logger.Information($"LogPath: {config["Serilog:WriteTo:0:Args:path"]}");
            //logger.Information($"LogMinimumLevel: {config["Serilog:MinimumLevel"]}");
            //logger.Information($"Location: {Assembly.GetEntryAssembly()?.Location}");
            //logger.Information($"Redis: {config["RedisConfig:ConnectionString"]}");
            //logger.Information("");

            //if (environmentName == "Development" || environmentName == "Test")
            //{
            //    logger = loggerConfiguration
            //        .MinimumLevel.ControlledBy(levelSwitch)
            //        .Enrich.With(new LogTelementryEnricher(config))
            //        .WriteTo.Console(theme: AnsiConsoleTheme.Code)
            //        .WriteTo.OpenTelemetry(options =>
            //        {
            //            options.Endpoint = "http://localhost:4317"; // Change this to match your OpenTelemetry Collector
            //            options.ResourceAttributes["service.name"] = ApiHelper.ServiceName;
            //        })
            //        .WriteTo.OpenTelemetry(options =>
            //        {
            //            /* Only export to OpenTelemetry collector */
            //            options.Endpoint = openTelemetryConfig?.LogsCollectorUrl; // Replace with your OTLP endpoint
            //            options.IncludedData = IncludedData.TraceIdField | IncludedData.SpanIdField;
            //            options.ResourceAttributes["service.name"] = ApiHelper.ServiceName;
            //            options.ResourceAttributes["service.version"] = ApiHelper.ServiceVersion;
            //        })
            //        .CreateLogger();
            //}
            //else
            //{
            //    logger = loggerConfiguration
            //        .MinimumLevel.ControlledBy(levelSwitch)
            //        .WriteTo.OpenTelemetry(options =>
            //        {
            //            options.Endpoint = openTelemetryConfig?.LogsCollectorUrl; // Change this to match your OpenTelemetry Collector
            //            options.ResourceAttributes["service.name"] = ApiHelper.ServiceName;
            //            options.ResourceAttributes["service.version"] = ApiHelper.ServiceVersion;
            //        })
            //        .CreateLogger();
            //}

            //services.AddLogging((builder) =>
            //{
            //    builder.ClearProviders();
            //    builder.AddSerilog(dispose: true);
            //});

            //services.AddSingleton<ILogger>(logger);
            //services.AddKeyedScoped<ILogger, ApiActivityLogger>(typeof(ApiActivityLogger).Name);
            //services.AddSingleton(levelSwitch);

            //Log.Logger = logger;

            //return services;
        }

        /// <summary>
        /// Configure Swagger
        /// </summary>
        public static IServiceCollection ConfigureSwagger(this IServiceCollection services, IConfiguration config)
        {
            var executingAssembly = Assembly.GetEntryAssembly();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            services.AddSwaggerGen(c =>
            {
                var assembly = Assembly.GetExecutingAssembly();
                var fileInfo = new FileInfo(assembly.Location);
                var modificationDate = fileInfo.LastWriteTimeUtc.ToShortDateString();

                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = executingAssembly?.GetName().Name,
                    Description = executingAssembly?.GetName().FullName,
                    Version = $"{config.GetValue<string>("Version")}  {config.GetValue<string>("Environment")}  {modificationDate}"
                });
                c.CustomSchemaIds(type => $"{type.Name}_{Guid.NewGuid()}");

                // get all xml document files
                foreach (var filePath in Directory.GetFiles(AppContext.BaseDirectory, "*.xml"))
                {
                    try
                    {
                        c.IncludeXmlComments(filePath);
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error($"SwaggerGen.IncludeXmlComments including file '{filePath}' error: {ex.InnerException?.Message ?? ex.Message}; stackTrace: {ex.StackTrace}");
                    }
                }
            });

            return services;
        }

        /// <summary>
        /// Configure ApiSettings settings
        /// </summary>
        public static void ConfigureAppSettings<TOptions>(this IConfiguration configuration, TOptions settings) where TOptions : ApiSettings
        {
            try
            {
                settings.Version = configuration.GetSection(nameof(settings.Version)).Get<string>()
                    ?? throw new ApiException($"{settings.Version} settings is not defined");
                settings.Optima = configuration.GetSection("OptimaConfig").Get<OptimaConfig>()!;
                settings.Redis = configuration.GetSection("RedisConfig").Get<RedisConfig>()!;
                settings.AzureStorage = configuration.GetSection("AzureStorageConfig").Get<AzureStorageConfig>()!;
                settings.Dal = configuration.GetSection("DalConfig").Get<DalConfig>()!;
                settings.OpenTelemetry = configuration.GetSection("OpenTelemetryConfig").Get<OpenTelemetryConfig>()!;
                settings.StaticData = configuration.GetSections(SettingKeys.StaticData.ToString()).ToList() ?? [];
                settings.Umbraco = configuration.GetSection("UmbracoConfig").Get<UmbracoConfig>()!;
                settings.Nop = configuration.GetSection("NopConfig").Get<NopConfig>()!;
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} error: {MESSAGE}", ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message);
                throw new ApiException(ex);
            }
        }

        public static IEnumerable<TData> ParseDataToList<TData>(this string data)
        {
            var dataItems = data?.ToString()?.Split(",") ?? [];
            return typeof(TData) == typeof(int)
               ? dataItems.Select(int.Parse).Cast<TData>().ToList()
               : dataItems.ToList().Cast<TData>();
        }

        /// <summary>
        /// get the name of the type
        /// </summary>
        public static T GetValueOrDefault<T>(this List<EntityData> dataSource, SettingKeys settingKey)
        {
            try
            {
                var source = (T?)(object?)dataSource.FirstOrDefault(x => x.SettingKey == settingKey)?.Value;
                if (ApiHelper.IsDataNullOrEmpty(source))
                    Log.Logger.Error("{TITLE} error: {SETTINGKEY} data source is null or empty",
                        ApiHelper.LogTitle(), settingKey);
                return source ?? (T)(object)settingKey.GetResourceType().GetDefault();
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                return typeof(T).IsAssignableTo(typeof(IEnumerable)) ? typeof(T).GetDefault<T>() : default!;
            }
        }

        /// <summary>
        /// GetValueOrDefault - get value from dictionary such as generic params with the ordinalIgnoreCase for key and value
        /// </summary>
        public static T GetValueOrDefault<T>(this Dictionary<string, string> dictionary, string key, T defaultValue)
            where T : IParsable<T>
        {
            var keyValuePair = dictionary.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (keyValuePair.Key is null)
                return defaultValue;

            var value = keyValuePair.Value;
            if (typeof(T) == typeof(bool))
                value = keyValuePair.Value.Equals("Y", StringComparison.OrdinalIgnoreCase) ? bool.TrueString : value;

            return T.TryParse(value, null, out var result) ? result : defaultValue;
        }
    }
}
