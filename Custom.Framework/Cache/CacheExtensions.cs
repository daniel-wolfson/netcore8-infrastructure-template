using Custom.Framework.Configuration;
using Custom.Framework.Contracts;
using Custom.Framework.Core;
using Custom.Framework.Helpers;
using Custom.Framework.Repositoty;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using System.Threading;
using Custom.Framework.Services;
using Custom.Framework.StaticData;

namespace Custom.Framework.Cache
{
    public static class CacheExtensions
    {
        public static IServiceCollection ConfigureCache(this IServiceCollection services, IConfiguration config)
        {
            // RedisConfig, string environmentName
            var redisConfig = config.GetSection("RedisConfig").Get<RedisConfig>()!;
            if (redisConfig != null)
            {
                services.Configure<RedisConfig>(cfg => config.GetSection("RedisConfig").Bind(cfg));
                services.AddSingleton<IOptionsMonitor<RedisConfig>, OptionsMonitor<RedisConfig>>();

                // Register the IConnectionMultiplexer
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                {
                    var redisConfig = sp.GetService<IOptions<RedisConfig>>()!.Value;
                    var configurationOptions = ConfigurationOptions.Parse(redisConfig.ConnectionString!, true);
                    configurationOptions.AbortOnConnectFail = false; // Allow the multiplexer to continue retrying
                    configurationOptions.ConnectTimeout = redisConfig.ConnectionTimeout * 1000; // ms

                    if (redisConfig?.TelemetryTraceEnabled == true)
                    {
                        var openTelemetryBuilder = sp.GetService<IOpenTelemetryBuilder>();
                        openTelemetryBuilder?.WithTracing(otelBuilder =>
                        {
                            var connectionMultiplexer = sp.GetService<IConnectionMultiplexer>();
                            if (connectionMultiplexer != null && connectionMultiplexer.IsConnected)
                                otelBuilder.AddRedisInstrumentation(connectionMultiplexer);
                        });
                    }

                    ConnectionMultiplexer connectionMultiplexer;
                    try
                    {
                        connectionMultiplexer = ConnectionMultiplexer.Connect(configurationOptions);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                            ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                        connectionMultiplexer = ConnectionMultiplexer.Connect(new ConfigurationOptions
                        {
                            EndPoints = { "nonexistent:6379" }, // Dummy configuration
                            AbortOnConnectFail = false,
                            ConnectTimeout = 500 // Short timeout to minimize impact
                        });
                    }

                    return connectionMultiplexer;
                });

                // Register redis ConfigurationOptions for connection
                services.AddSingleton(provider =>
                {
                    var configuration = provider.GetService<IConfiguration>();
                    var configurationOptions = ConfigurationOptions.Parse(redisConfig.ConnectionString!, true);
                    configurationOptions.AbortOnConnectFail = false;
                    return configurationOptions;
                });

                services.AddTransient<IRetryPolicyBuilder>((sp) =>
                {
                    var redisConfig = sp.GetService<IOptions<ApiSettings>>()?.Value.Redis;
                    var retryPolicyBuilder = new ApiRetryPolicyBuilder()
                       .WithRetryCount(redisConfig?.RetryAttempts ?? 3)
                       .WithRetryInterval(TimeSpan.FromSeconds(redisConfig?.RetryInterval ?? ApiRetryPolicyBuilder.RetryIntervalDefault.TotalSeconds))
                       .WithRetryTimeout();
                    return retryPolicyBuilder;
                });

                services.AddSingleton<IRedisCache, RedisCache>();
            }

            services.AddSingleton<IApiCacheOptions, ApiMemoryCacheOptions>();
            services.AddSingleton<IBlobStorage, BlobStorage>();

            var environmentName = config.GetValue<string>("environment");
            if (environmentName == "Test")
                services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
            else
                services.AddMemoryCache();

            services.AddScoped<IReloadCacheTask, ReloadCacheTask>();
            services.AddHostedService<ReloadCacheService>();

            return services;
        }
    }
}