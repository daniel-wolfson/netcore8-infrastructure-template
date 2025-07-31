using Azure.Storage.Blobs;
using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Exceptions;
using Custom.Framework.Helpers;
using Custom.Framework.Middleware;
using Custom.Framework.Models;
using Custom.Framework.Models.Base;
using Custom.Framework.Repositoty;
using Custom.Framework.Services;
using Custom.Framework.StaticData.Confiuration;
using Custom.Framework.StaticData.Contracts;
using Custom.Framework.StaticData.DbContext;
using Custom.Framework.StaticData.Middleware;
using Custom.Framework.StaticData.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using System.Threading;
using ConfigurationManager = Microsoft.Extensions.Configuration.ConfigurationManager;

namespace Custom.Framework.StaticData.Extensions
{
    public static class ApiExtensions
    {
        public static StaticDataCollection<EntityData> InitStaticDataCollection(
            this StaticDataCollection<EntityData> staticDataCollection,
            string sectionName, int cacheTtl)
        {

            // init staticData collection
            //staticDataCollection.EnsureCacheInitialized();
            //Task.Run(() => staticDataCollection.StartCacheReload());

            //// get app sections by sectionName
            //var sections = staticDataCollection.Configuration.GetSection(sectionName).Get<IEnumerable<ConfigData>>() ?? [];

            //// add settingKey to staticData collection
            //foreach (var item in sections)
            //{
            //    if (Enum.TryParse<SettingKeys>(item.Name, out var settingKey))
            //    {
            //        // set settingKey if not defined
            //        if (item.SettingKey == SettingKeys.Unknown)
            //            item.SettingKey = settingKey;
            //    }
            //    // set order if not defined
            //    item.Order = item.Order == 0 ? int.MaxValue : item.Order;
            //    staticDataCollection.Add(item);
            //}

            return staticDataCollection;
        }

        /// <summary>
        /// ConfigureStaticData - registers several services
        /// </summary>
        public static IServiceCollection ConfigureStaticData(this IServiceCollection services)
        {
            services.AddSingleton<IStaticDataRepository, StaticDataRepository>();
            services.AddSingleton<StaticDataCollection<EntityData>>();
            services.AddSingleton<StaticDataService>();
            
            services.AddSingleton<IStaticDataService, StaticDataService>(sp =>
            {
                var staticDataService = sp.GetRequiredService<StaticDataService>();
                var collection = sp.GetRequiredService<StaticDataCollection<EntityData>>();
                collection.SetParent(staticDataService);
                return staticDataService;
            });

            services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var appSettings = sp.GetRequiredService<IOptions<ApiSettings>>().Value;
                var client = new BlobServiceClient(appSettings.AzureStorage.ConnectionString);
                return client;
            });

            services.AddScoped<RequestMiddlewareOptions>();
            services.Configure<RequestMiddlewareOptions>(options =>
            {
                options.StaticDataCancellationSource = new CancellationTokenSource();
                options.StaticDataCancellationToken = options.StaticDataCancellationSource.Token;
            });

            return services;
        }

        /// <summary>
        /// UseStaticData - load static data
        /// Use settings from external sources, such as, AzureStorage, Db, Apis
        /// and give use Unknown object as regular access to settings
        /// </summary>
        public static IApplicationBuilder UseStaticData(this IApplicationBuilder builder, string providerTypes = ProviderTypes.All)
        {
            try
            {
                builder.UseMiddleware<ApiStaticDataLoadMiddleware>(providerTypes);

                var app = (WebApplication)builder;
                // Ensure database is created after app runs
                var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
                lifetime.ApplicationStarted.Register(async () =>
                {
                    _ = Task.Run(async () =>
                    {
                        //    try
                        //    {
                        //        //using var scope = app.Services.CreateScope();
                        //        //var staticDataService = scope.ServiceProvider.GetService<IStaticDataService>();
                        //        //var data = await staticDataService?.LoadStaticData()!;
                        //        //staticDataService.SetToMemoryCache(data);
                        //        //var dbContext = scope.ServiceProvider.GetRequiredService<OptimaContext>();
                        //        //dbContext.Database.EnsureCreated();
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        Console.WriteLine($"Error during database initialization: {ex.Message}");
                        //    }
                    });
                });
            }
            catch (Exception ex)
            {
                throw new ApiException(ServiceStatus.FatalError, ex);
            }
            return builder;
        }

        /// <summary>
        /// UseStaticDataPreloadConfiguration - preload static data
        /// </summary>
        public static IApplicationBuilder UseStaticDataPreloadConfiguration(this IApplicationBuilder builder)
        {
            try
            {
                builder.UseMiddleware<ApiStaticDataPreloadMiddleware>();
            }
            catch (Exception ex)
            {
                throw new ApiException(ServiceStatus.FatalError, ex);
            }
            return builder;
        }

        /// <summary> 
        /// DataContext - init configuration sources
        /// </summary>
        public static IApplicationBuilder UseStaticDataInitConfiguration(this IApplicationBuilder builder)
        {
            try
            {
                var app = (WebApplication)builder;
                var factory = app.Services.GetService<IApiConfigurationFactory>()!;
                factory.InitConfigurationSources(SettingKeys.StaticData).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} failed: {EXCEPTION}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                throw new ApiException(ServiceStatus.FatalError, ex);
            }
            return builder;
        }

        public static IServiceCollection ConfigureReloadStaticData<TOptions>(this IServiceCollection services,
            IConfiguration config, Action<TOptions>? configureOptions = null)
            where TOptions : ApiSettings
        {
            services.AddSingleton<ReloadTimerService>();
            services.AddSingleton<ReloadWorkerService>();
            services.AddSingleton<IApiReloadTaskQueue, ApiReloadTaskQueue>();
            return services;
        }

        public static IServiceCollection ConfigureOptimaContext(this IServiceCollection services, IConfigurationManager config)
        {
            services.AddDbContext<OptimaContext>(options =>
            {
                options.UseInMemoryDatabase("OptimaStaticData");
                //options.EnableServiceProviderCaching();
                //options.UseInternalServiceProvider(serviceProvider);
            }, ServiceLifetime.Singleton);

            services.AddSingleton(serviceProvider =>
            {
                var options = new ApiDbContextOptions { ServiceProvider = serviceProvider };
                return options;
            });

            return services;
        }

        /// <summary>
        /// Configure configuration sources
        /// </summary>
        public static IServiceCollection ConfigureDataSources(this IServiceCollection services,
            IConfiguration config)
        {
            //services.Configure<AzureStorageOptions>(config => config.GetSections("AzureStorageConfig").Bind(config));
            services.AddSingleton<IApiConfigurationFactory, ApiConfigurationFactory>();
            services.AddTransient<IConfigurationSource, ApiConfigurationSource>();

            // Get the StaticList settings from app settings
            var settings = config!.GetSections(SettingKeys.StaticData).ToDataList();

            // Init source for each of setting section
            settings.ForEach(setting =>
            {
                services.AddKeyedSingleton<IConfigurationSource>(setting.SettingKey,
                    (sp, key) =>
                    {
                        var configuration = sp.GetService<IConfiguration>()!;
                        var settingKeyAttr = setting.SettingKey.GetDisplayAttribute();

                        SettingKeys settingRootKey = SettingKeys.StaticData;
                        _ = System.Enum.TryParse(settingKeyAttr.GroupName ?? "", out settingRootKey);

                        var source = (ApiConfigurationSource)sp.GetService<IConfigurationSource>()!;
                        source.SettingKey = (SettingKeys)key!;
                        source.RootSettingKey = settingRootKey;
                        source.ResourceType = setting.SettingKey.GetResourceType().Name;
                        source.ReloadInterval = setting.ReloadInterval;
                        source.ReloadTimeout = setting.ReloadTimeout;
                        source.SourceType = setting.SourceType;
                        source.Order = setting.Order;
                        return source;
                    });
            });

            foreach (var section in config.GetSection("StaticList").GetChildren())
            {
                services.AddKeyedScoped(section.Key, (sp, k) =>
                {
                    return sp.GetRequiredService<IOptionsSnapshot<ConfigData>>();
                });

                services.Configure<ConfigData>(section.Key, sect =>
                {
                    var configurationSection = config.GetSection($"StaticList:{section.Key}");
                    configurationSection.Bind(sect);
                });
            }

            // Init provider fro each setting source
            settings.ForEach(section =>
            {
                var providerType = section.SourceType.GetResourceType();
                if (providerType != null)
                {
                    services.AddKeyedSingleton<IConfigurationProvider, ApiConfigurationProvider>(section.SettingKey,
                        (sp, key) =>
                        {
                            var settingKey = (SettingKeys)key;
                            // get ConfigurationSource
                            var source = (ApiConfigurationSource)sp.GetKeyedService<IConfigurationSource>(settingKey)!;
                            source.InitialData = [];

                            //var provider = section.SourceType switch
                            //{
                            //    ProviderTypes.Umbraco => new ApiUmbracoConfigurationProvider(sp.GetRequiredService<IServiceScopeFactory>(), source),
                            //    ProviderTypes.Optima => new ApiOptimaConfigurationProvider(sp.GetRequiredService<IServiceScopeFactory>(), source),
                            //    ProviderTypes.Dal => new ApiDbConfigurationProvider(sp.GetRequiredService<IServiceScopeFactory>(), source),
                            //    _ => new ApiConfigurationProvider(sp.GetRequiredService<IServiceScopeFactory>(), source),
                            //};
                            var provider = new ApiConfigurationProvider(sp.GetRequiredService<IServiceScopeFactory>(), source);

                            //(ApiConfigurationProvider)sp.GetRequiredKeyedService(providerType, settingKey)!;
                            // init key
                            provider.Set($"{SettingKeys.StaticData}:{settingKey}", source.SettingKey.GetJsonDefault());

                            return provider;
                        });
                }
                else
                {
                    Log.Logger.Warning("{TITLE} warning: unknown source type", ApiHelper.LogTitle());
                }
            });

            //services.AddHostedService<ReloadTimerService>();
            //services.AddHostedService<ReloadWorkerService>();
            //services.AddSingleton<ReloadTimerService>();
            //services.AddSingleton<ReloadWorkerService>();
            //services.AddSingleton<IApiReloadTaskQueue, ApiReloadTaskQueue>();

            services.AddScoped<RequestMiddlewareOptions>();
            services.Configure<RequestMiddlewareOptions>(options =>
            {
                options.StaticDataCancellationSource = new CancellationTokenSource();
                options.StaticDataCancellationToken = options.StaticDataCancellationSource.Token;
            });

            services.AddSingleton(config);
            services.AddSingleton<IConfigurationManager>((ConfigurationManager)config);
            return services;
        }
    }
}