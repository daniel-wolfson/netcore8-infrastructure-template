using AutoMapper.Internal;
using Custom.Domain.Optima.Models.Nop;
using Custom.Framework.Cache;
using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Configuration.Optima;
using Custom.Framework.Configuration.Umbraco;
using Custom.Framework.Contracts;
using Custom.Framework.Core;
using Custom.Framework.Extensions;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Custom.Framework.Models.Base;
using Custom.Framework.Services;
using Custom.Framework.StaticData.Contracts;
using Custom.Framework.StaticData.DbContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Custom.Framework.StaticData.Services
{
    public class StaticDataService(
        ILogger logger,
        IConfiguration config,
        IMemoryCache memoryCache,
        IRedisCache redisCache,
        IBlobStorage blobStorage,
        IStaticDataRepository staticDataRepository,
        StaticDataCollection<EntityData> staticDataCollection,
        IApiHttpClientFactory apiHttpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ApiSettings> appSettingsOptions) : ApiWorkerBase, IStaticDataService
    {
        private readonly ApiSettings apiSettings = appSettingsOptions.Value;
        private MemoryCacheEntryOptions _memoryCacheEntryOptions;
        public IServiceScope StaticDataServiceScope => serviceScopeFactory.CreateScope();

        /// <summary>
        /// UseMemoryCache - use memory cache, default is value UseMemoryCache from app settings
        /// </summary>
        public bool UseMemoryCache { get; set; } = true;

        /// <summary> Exist staticData in memory cache </summary>
        public List<string> UndefinedStaticData =>
            DataContext?.Where(x => x.Value == null).Select(x => x.Name).ToList() ?? [];

        /// <summary>
        /// InMemoryStaticData - get static data from memory cache
        /// </summary>
        public List<string> InMemoryStaticData =>
            DataContext?
                .Where(item => memoryCache.TryGetValue(GenerateCacheKey(item.SettingKey), out _))
                .Select(item => item.Name).ToList() ?? [];

        private string GenerateCacheKey(SettingKeys settingKey) => $"{SettingKeys.StaticData}_{settingKey}";

        /// <summary>
        /// StaticData app settings
        /// </summary>
        private List<ConfigData>? _dataSections;
        
        public List<ConfigData> DataSections
        {
            get => _dataSections ??= config.GetSections(SettingKeys.StaticData).ToList() ?? [];
            set => _dataSections = value;
        }

        /// <summary> 
        /// EntityData context - it was loaded in middleware 
        /// (see, ApiStaticDataLoadMiddleware - the best way to use it is to call ApiStaticDataLoadMiddleware HandleLoadData method)
        /// if it is null on run time, then will occurd the lazy loading directly by calling LoadStaticData
        ///</summary>
        public StaticDataCollection<EntityData> DataContext => staticDataCollection;

        #region Public methods

        private Task? _backgroundTask;
        public void InitOptimaContext(IServiceProvider serviceProvider)
        {
            // Prevent starting the task multiple times
            if (_backgroundTask != null && !_backgroundTask.IsCompleted)
                return;

            _backgroundTask = Task.Run(async () =>
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<OptimaContext>();

                    var data = await dbContext.Hotels.ToListAsync();
                    Console.WriteLine($"Preloaded {data.Count} entities.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during background initialization: {ex.Message}");
                }
            });
        }

        public Task GetTask() => _backgroundTask ?? Task.CompletedTask;

        /// <summary>
        /// GetFromStaticList - get value from list
        /// </summary>
        public IServiceResult<List<EntityData>> GetFromStaticList(IEnumerable<string>? settingKeys = null)
        {
            try
            {
                var sections = GetSettings(settingKeys, ProviderTypes.StaticList);

                var dataResults = sections.Select(setting =>
                {
                    var dataResult = config
                        .GetValue<string>($"StaticData:{setting.Name}:Data")?.Split(',')
                        .Select(x => x.Trim())
                        .ToList() ?? [];

                    var itemDataResult = new EntityData(setting.Name, false, "", dataResult)
                    {
                        Value = dataResult.All(ApiHelper.IsDigitsOnly)
                            ? dataResult.Select(int.Parse).ToList()
                            : dataResult,
                    };
                    itemDataResult.Error = ApiHelper.IsDataNullOrEmpty(itemDataResult.Value);

                    return itemDataResult;

                })?.ToList();

                return dataResults != null && dataResults.Count > 0
                    ? ServiceResult<List<EntityData>>.Ok(dataResults)
                    : ServiceResult<List<EntityData>>.NoData();
            }
            catch (Exception ex)
            {
                logger.Error("{TITLE}. ErrorInfo: {MESSAGE}. ST: {STACKTRACE}", ApiHelper.LogTitle(),
                    ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                return ServiceResult<List<EntityData>>
                    .Error($"{ApiHelper.LogTitle()}. ErrorInfo: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        /// <summary>
        /// GetFromBlobAsync - get value by keys from Blob
        /// </summary>
        public async Task<IServiceResult<List<EntityData>>> GetFromBlobAsync(string version, IEnumerable<string>? settingKeys = null)
        {
            try
            {
                var settings = GetSettings(settingKeys);

                var stepName = Guid.NewGuid();
                logger.Verbose("GetFromBlob-step-{STEPNAME} starting for settingKeys: {SETTINGKEYS}",
                    stepName, string.Join(", ", settings.Select(x => x.Name).ToArray()));

                var loadTasks = settings
                    .Select(async setting =>
                    {
                        if (setting.Provider != ProviderTypes.StaticList)
                        {
                            var blobName = setting.SettingKey.ToString();
                            var resourceType = setting.SettingKey.GetResourceType();
                            var result = await blobStorage.DownloadAsync(version, blobName, resourceType);

                            if (result.IsSuccess && result.Value != null)
                            {
                                setting.Data = JsonConvert.SerializeObject(result.Value);
                                setting.Value = result.Value;
                                setting.Error = ApiHelper.IsDataNullOrEmpty(result.Value);
                            }
                            else
                            {
                                setting.Error = true;
                            }
                        }
                        return setting;
                    });

                var blobResults = await Task.WhenAll(loadTasks);

                // load static appsettings data
                var finalResults = blobResults.Cast<EntityData>().ToDataList();

                logger.Verbose("GetFromBlob-step-{STEPNAME} finished successfully.", stepName);

                return Ok(finalResults);
            }
            catch (Exception ex)
            {
                logger.Error("{TITLE}. ErrorInfo: {MESSAGE}. ST: {STACKTRACE}", ApiHelper.LogTitle(),
                    ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                return ServiceResult<List<EntityData>>
                    .Error($"{ApiHelper.LogTitle()}. ErrorInfo: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        /// <summary>
        /// GetFromNop - get value from Umbraco
        /// </summary>
        public async Task<IServiceResult<List<EntityData>>> GetFromNop()
        {
            try
            {
                var client = apiHttpClientFactory.CreateClient(ApiHttpClientNames.NopApi);
                var path = config.GetValue<string>($"StaticData:NopSettings:Path") ?? "";
                var result = await client.GetAsync(path);
                var value = result?.Value != null ? JsonConvert.DeserializeObject<NopSearchSettings>(result.Value) : default;
                var entityData = new EntityData()
                {
                    Name = "Nop",
                    Value = value,
                    Data = null,
                    Error = ApiHelper.IsDataNullOrEmpty(value)
                };
                return Ok(new List<EntityData>() { entityData });
            }
            catch (Exception ex)
            {
                logger.Error("{TITLE}. ErrorInfo: {MESSAGE}. ST: {STACKTRACE}", ApiHelper.LogTitle(),
                    ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                return ServiceResult<List<EntityData>>
                    .Error($"{ApiHelper.LogTitle()}. ErrorInfo: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        /// <summary>
        /// GetCurrencyRatesAsync - get value from Db
        /// </summary>
        public async Task<IServiceResult<List<EntityData>>> GetCurrencyRates()
        {
            try
            {
                var client = apiHttpClientFactory.CreateClient(ApiHttpClientNames.DalApi);
                var path = config.GetValue<string>("StaticData:CurrencyRates:Path");

                if (path == null)
                    return ServiceResult<List<EntityData>>.Error("StaticData currencyRates key path not defined");

                var result = await client.GetAsync<List<CurrencyRate>>(path);
                var data = result?.Value ?? default;

                var entityData = new EntityData() { Name = "Db", Data = data, Error = ApiHelper.IsDataNullOrEmpty(data) };

                return Ok(new List<EntityData>() { entityData });
            }
            catch (Exception ex)
            {
                logger.Error("{TITLE}. ErrorInfo: {MESSAGE}. ST: {STACKTRACE}", ApiHelper.LogTitle(),
                    ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                return ServiceResult<List<EntityData>>
                    .Error($"{ApiHelper.LogTitle()}. ErrorInfo: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        /// <summary>
        /// GetCurrencyRatesAsync - get value from Db
        /// </summary>
        public async Task<IServiceResult<List<EntityData>>> GetCurrencyRatesAsync(IEnumerable<string>? settingKeys = null)
        {
            try
            {
                var client = apiHttpClientFactory.CreateClient(ApiHttpClientNames.DalApi);
                var sections = GetSettings(settingKeys, ProviderTypes.Dal).ToDataList();
                var loadTasks = sections.Select(async sectionData =>
                {
                    var path = config.GetValue<string>($"StaticData:{sectionData.Name}:Path")!;
                    var result = await client.GetAsync(path);
                    var value = result?.Value != null ? JObject.Parse(result.Value) : default;
                    var dataResult = new DataResult(sectionData.Name, result?.IsSuccess ?? false, result?.Message ?? "", value, result?.Value);
                    dataResult.Error = ApiHelper.IsDataNullOrEmpty(value);
                    return dataResult;
                });

                var loadDataResults = await Task.WhenAll(loadTasks);
                return Ok(loadDataResults.Cast<EntityData>().ToList());
            }
            catch (Exception ex)
            {
                logger.Error("{TITLE}. ErrorInfo: {MESSAGE}. ST: {STACKTRACE}", ApiHelper.LogTitle(),
                    ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                return ServiceResult<List<EntityData>>
                    .Error($"{ApiHelper.LogTitle()}. ErrorInfo: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        /// <summary>
        /// GetFromUmbraco - get value from Umbraco
        /// </summary>
        public async Task<IServiceResult<List<EntityData>>> GetFromUmbraco(IEnumerable<string>? settingKeys = null)
        {
            try
            {
                var settings = GetSettings(settingKeys);
                var staticSections = GetSettings(ProviderTypes.StaticList).ToDataList();
                var sections = GetSettings(settingKeys, ProviderTypes.Umbraco);

                var rootNodeIds = staticSections.FirstOrDefault(x => x.SettingKey == SettingKeys.RootNodeIds)?
                   .Value as List<int> ?? [];

                var settingTasks = sections.Select(async setting =>
                {
                    Enum.TryParse(setting.Name, out SettingKeys settingKey);
                    string path;
                    try
                    {
                        var client = apiHttpClientFactory.CreateClient(ApiHttpClientNames.UmbracoApi);
                        switch (settingKey)
                        {
                            case SettingKeys.UmbracoSettings:
                                var umbracoTasks = rootNodeIds.Select(async rootNodeId =>
                                {
                                    path = config.GetValue<string>($"StaticData:{settingKey}:Path")?.Replace("XXXXX", rootNodeId.ToString())!;
                                    var dataResult = await client.GetAsync<UmbracoSettings>(path);
                                    var dataItem = dataResult.Value;
                                    if (dataItem != null) dataItem.Id = rootNodeId;
                                    return dataItem;
                                });
                                var umbracoResults = await Task.WhenAll(umbracoTasks);
                                setting.Error = umbracoResults.All(x => ApiHelper.IsDataNullOrEmpty(x));
                                setting.Value = umbracoResults.Cast<object>().ToList();
                                setting.Message = setting.Error ? "umbracoResults are empty" : "Success";
                                break;

                            case SettingKeys.OptimaSettings:
                                path = config.GetValue<string>($"StaticData:{settingKey}:Path")!;
                                path = $"{path}?rootNodeIds={string.Join(",", rootNodeIds)}";
                                var optimaResult = await client.GetAsync<OptimaSettings>(path);
                                setting.Value = optimaResult?.Value;
                                setting.Error = ApiHelper.IsDataNullOrEmpty(optimaResult);
                                setting.Message = setting.Error ? "OptimaSettings is empty" : "Success";
                                break;

                            default:
                                path = config.GetValue<string>($"StaticData:{settingKey}:Path")!;
                                var _dataResult = await client.GetAsync<PmsSettings>(path);
                                setting.Value = _dataResult?.Value;
                                setting.Error = ApiHelper.IsDataNullOrEmpty(_dataResult);
                                setting.Message = setting.Error ? "PmsSettings is empty" : "Success";
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        var errMsg = ex.InnerException?.Message ?? ex.Message;
                        logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                            ApiHelper.LogTitle(), errMsg, ex.StackTrace);
                        setting.Message = errMsg;
                    }
                    return setting;
                });

                var loadUmbracoSettingKeysResults = await Task.WhenAll(settingTasks);
                var results = loadUmbracoSettingKeysResults.Cast<EntityData>().ToList() ?? [];

                return Ok(results);
            }
            catch (Exception ex)
            {
                logger.Error("{TITLE}. ErrorInfo: {MESSAGE}. ST: {STACKTRACE}", ApiHelper.LogTitle(),
                    ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                return ServiceResult<List<EntityData>>
                    .Error($"{ApiHelper.LogTitle()}. ErrorInfo: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        /// <summary>
        /// GetFromOptima - get value by keys from Optima
        /// </summary>
        public async Task<IServiceResult<List<EntityData>>> GetFromOptima(IEnumerable<string>? settingKeys = null)
        {
            try
            {
                var optimaSections = GetSettings(settingKeys, ProviderTypes.Optima).ToDataList();

                List<ConfigData>? dependsOnEntities = default; //  entities with dependsOn
                List<ConfigData>? dependsOffEntities = default; // entities without dependencies

                // make dependsOnList
                var dependsOnList = optimaSections
                    .Where(x => !string.IsNullOrEmpty(x.DependsOn))
                    .DistinctBy(x => x.DependsOn)
                    .ToList();

                // load dependsOn entities
                if (dependsOnList.Count != 0)
                {
                    dependsOnEntities = DataSections
                     .Where(x => dependsOnList.Select(d => d.DependsOn).Contains(x.Name))
                     .DistinctBy(x => x.Name)
                     .ToList();

                    foreach (var x in dependsOnEntities)
                    {
                        if (x.Provider != null && x.Provider.StartsWith(ProviderTypes.Optima))
                        {
                            var optimaResults = await LoadFromOptima(x);
                            x.Value = optimaResults?.Data;
                            x.Data = null;
                        }
                    }
                }

                // load dependsOn entities
                dependsOffEntities = optimaSections
                    .Where(x => dependsOnEntities == null || !dependsOnEntities.Select(d => d.Name).Contains(x.Name))
                    .DistinctBy(x => x.Name)
                    .ToList();

                var dependsOnEntitiesTasks = dependsOffEntities.Select(LoadFromOptima).ToList();
                dependsOffEntities = (await Task.WhenAll(dependsOnEntitiesTasks)).ToList();

                foreach (var x in dependsOffEntities)
                {
                    if (x.Provider != null && x.Provider.StartsWith(ProviderTypes.Optima))
                    {
                        x.Value = x.Data;
                        x.Data = null;
                    }
                }

                // save value to configuration
                //await SetUpConfig(dependsOffEntities);

                // make entities
                var finalEntityResults = (dependsOnEntities != null && dependsOnEntities.Any())
                    ? dependsOffEntities?.Concat(dependsOnEntities).ToList() ?? []
                    : dependsOffEntities;

                // make final resultsValues
                var finalResults = finalEntityResults?
                    .Select(sectionData =>
                    {
                        sectionData.Error = ApiHelper.IsDataNullOrEmpty(sectionData.Value);
                        sectionData.Message = $"Setting {sectionData.SettingKey} {(sectionData.Error ? sectionData.Message : "is valid")}";
                        return sectionData;
                    })
                    .Cast<EntityData>()
                    .ToList();

                return Ok(finalResults);
            }
            catch (Exception ex)
            {
                logger.Error("{TITLE}. ErrorInfo: {MESSAGE}. ST: {STACKTRACE}", ApiHelper.LogTitle(),
                    ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                return ServiceResult<List<EntityData>>
                    .Error($"{ApiHelper.LogTitle()}. ErrorInfo: {ex.Message}");
            }
        }

        /// <summary>
        /// GetFromRedis - get value from Redis
        /// </summary>
        public async Task<IServiceResult<List<EntityData>>> GetFromRedis(string version, IEnumerable<string>? settingKeys = null)
        {
            try
            {
                var sections = GetSettings(settingKeys).ToDataList();

                var tasks = sections
                    .Select(async x =>
                    {
                        var redisKey = $"{SettingKeys.StaticData}_{version}:{x.Name}";

                        var redisData = await redisCache.GetAsync(redisKey, x.SettingKey.GetResourceType());
                        var errMsg = redisCache.GetLatestError()?.ToString();
                        x.Error = ApiHelper.IsDataNullOrEmpty(redisData);
                        x.Value = !x.Error ? redisData : null;
                        x.ResourceType = x.SettingKey.GetResourceType().ToString();
                        x.Data = null;
                        x.Message = !x.Error && string.IsNullOrEmpty(errMsg)
                            ? $"Key {x.Name} cached successfully"
                            : $"Key {x.Name} not cached: {errMsg}";
                        return x;
                    });

                var dataResults = await Task.WhenAll(tasks);
                return Ok(dataResults.Cast<EntityData>().ToList());
            }
            catch (Exception ex)
            {
                logger.Error("{TITLE}. ErrorInfo: {MESSAGE}. ST: {STACKTRACE}", ApiHelper.LogTitle(),
                    ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                return ServiceResult<List<EntityData>>
                    .Error($"{ApiHelper.LogTitle()}. ErrorInfo: {ex.InnerException?.Message ?? ex.Message}");
            }

        }

        /// <summary>
        /// UpdateFromBlobsToRedis - get value by keys from Blob and upload to Redis
        /// </summary>
        public async Task<IServiceResult<List<Result>>> UpdateToRedis(string version, IEnumerable<string>? settingKeys = null)
        {
            try
            {
                // get from blob
                var blobServiceResult = await GetFromBlobAsync(version, settingKeys);

                // no value
                if (blobServiceResult == null || blobServiceResult.Value == null)
                    return ServiceResult<List<Result>>.NoData();

                var dataResults = blobServiceResult?.Value ?? [];

                var tasks = dataResults
                    .Select(async x =>
                    {
                        var cacheKey = $"{CacheKeys.StaticData}_{version}:{x.Name}";
                        var isSuccess = await redisCache.SetAsync($"{cacheKey}", x.Value, TTL.OneDay);
                        x.Error = isSuccess && ApiHelper.IsDataNullOrEmpty(x.Value);
                        //var errMsg = redisCache.GetLatestError()?.ToString() ?? "cached failed";
                        x.Message = !x.Error && !ApiHelper.IsDataNullOrEmpty(x.Value)
                            ? $"Key {x.Name} cached successfully"
                            : $"Key {x.Name} not cached: {x.Message}";
                        return x;
                    });

                var results = await Task.WhenAll(tasks);
                var finalResults = results.OrderBy(x => x.Name).Cast<Result>().ToList();
                return Ok(finalResults);
            }
            catch (Exception ex)
            {
                logger.Error("{TITLE}. ErrorInfo: {MESSAGE}. ST: {STACKTRACE}", ApiHelper.LogTitle(),
                    ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                return ServiceResult<List<Result>>
                    .Error($"{ApiHelper.LogTitle()}. ErrorInfo: {ex.Message}");
            }
        }

        /// <summary>
        /// UpdateToBlob - get value by keys from Optima and upload to Blob (TEMP - potentially to delete)
        /// </summary>
        public async Task<IServiceResult<List<Result>>> UpdateToBlob(string version, IEnumerable<string>? settingKeys = null)
        {
            try
            {
                // get data from settings
                var optimaServiceResult = await GetFromOptima(settingKeys);

                // no value result
                if (optimaServiceResult == null || optimaServiceResult.Value == null)
                    return ServiceResult<List<Result>>.NoData();

                var dataResults = optimaServiceResult?.Value ?? [];

                // init Upload BlobTasks
                var tasks = dataResults
                    .Select(async x =>
                    {
                        var isSuccess = await blobStorage.UploadAsync(version, x.Name, x.Value);
                        x.Error = isSuccess && ApiHelper.IsDataNullOrEmpty(x.Value);
                        x.Message = !x.Error && !ApiHelper.IsDataNullOrEmpty(x.Value)
                            ? $"Key {x.Name} uploaded successfully"
                            : $"Key {x.Name} not uploaded: {x.Message}";
                        return x;
                    });

                var results = await Task.WhenAll(tasks);
                var finalResults = results.Cast<Result>().OrderBy(x => x.Name).ToList();
                return Ok(finalResults);
            }
            catch (Exception ex)
            {
                logger.Error("{TITLE}. ErrorInfo: {MESSAGE}. ST: {STACKTRACE}", ApiHelper.LogTitle(),
                    ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                return ServiceResult<List<Result>>
                    .Error($"{ApiHelper.LogTitle()}. ErrorInfo: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public async Task<object> GetOrCreateData(SettingKeys settingKey, Func<ICacheEntry, Task<object>> factory = null)
        {
            var cacheKey = $"{CacheKeys.StaticData}_{apiSettings.Version}:{settingKey}";

            return await memoryCache.GetOrCreateAsync(cacheKey, factory);
            //async entry =>
            //{
            //    var resourceType = settingKey.GetResourceType();
            //    try
            //    {
            //        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttl);
            //        var redisCacheKey = $"{CacheKeys.staticDataSource}_{AppSettings.Version}:{settingKey}";
            //        var redisValue = await RedisCache.GetAsync(redisCacheKey, resourceType);
            //        return redisValue ?? settingKey.GetResourceType().GetDefault();
            //    }
            //    catch (Exception ex)
            //    {
            //        Logger.Error("{TITLE} exception: {MESSAGE}. \nStackTrace: {STACKTRACE}\n",
            //            ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            //        return settingKey.GetResourceType().GetDefault();
            //    }
            //});
        }

        /// <summary>
        /// LoadStaticData - get value value by the Appsettings staticDataSource group and setup to config (IConfiguration)
        /// </summary>
        public async Task LoadStaticData(string providerType = ProviderTypes.All, IEnumerable<string>? settingKeys = null)
        {
            try
            {
                // LoadStaticData-step-01 get value from MemoryCache or RedisCache
                await LoadCachedStaticData(settingKeys);

                // LoadStaticData-step-02 get Data from StaticList
                if (DataContext.Any(x => x.Provider == ProviderTypes.StaticList && x.Value == null))
                {
                    LoadAppSettingStaticData();
                }

                // LoadStaticData-step-03 get Data from External StaticDataService
                if (DataContext.Any(x => x.Value == null && providerType != ProviderTypes.AllPreloadStaticList))
                {
                    await LoadExternalStaticData(providerType);
                }

                if (UndefinedStaticData.Count > 0)
                {
                    logger.Warning("{TITLE} error: Exists undefined settings: {UNDEFINEDSETTINGS}",
                        ApiHelper.LogTitle(), string.Join(", ", UndefinedStaticData));
                }
            }
            catch (Exception ex)
            {
                logger.Error("{TITLE} failed. Exception: {ERRORMESSAGE}. StackTrace: {STACKTRACE}",
                    ApiHelper.LogTitle(), ex.Message, ex.StackTrace);
            }
        }

        public async Task PreloadStaticData(string providerType = ProviderTypes.All, IEnumerable<string>? settingKeys = null)
        {
            List<ConfigData>? staticDataSource = default;
            try
            {
                // staticData-step-01 get value by keys from AppSettings
                staticDataSource = GetSettings(settingKeys, providerType).ToDataList();

                // staticData-step-02 get value from MemoryCache or RedisCache
                await LoadCachedStaticData();

                // staticData-step-03 get Data from StaticList
                if (UndefinedStaticData.Any())
                    LoadAppSettingStaticData();

                // staticData-step-04 get Data from External StaticDataService
                if (UndefinedStaticData.Count > 0)
                    await PreloadExternalStaticData(providerType);

                if (UndefinedStaticData.Count > 0)
                {
                    logger.Warning("{TITLE} error: Exists undefined settings: {UNDEFINEDSETTINGS}",
                        ApiHelper.LogTitle(), string.Join(", ", UndefinedStaticData));
                }
            }
            catch (Exception ex)
            {
                logger.Error("{TITLE} ({BASEADDRESS}) failed. ErrorInfo: {ERRORMESSAGE}. StackTrace: {STACKTRACE}",
                        ApiHelper.LogTitle(), ex.Message, ex.StackTrace);
            }
        }

        /// <summary>
        /// SetToMemoryCache - set value to MemoryCache
        /// </summary>
        public void SetToMemoryCache(IEnumerable<string>? settingKeys = null)
        {
            if (!UseMemoryCache)
                return;

            DataContext.SetMemoryCache(settingKeys);

            foreach (var item in DataContext)
            {
                if (item != null && !ApiHelper.IsDataNullOrEmpty(item.Value))
                {
                    var cacheEntryExist = memoryCache.TryGetValue(item.SettingKey, out var cacheEntry);
                    var isDebugMode = httpContextAccessor.HttpContext?.IsRequestDebugMode() ?? false;
                    if (isDebugMode)
                    {
                        // add event to activity
                        Activity.Current?.AddEvent(
                            new ActivityEvent($"StaticDataMemoryCacheUpdated",
                                DateTimeOffset.Now,
                                tags: new ActivityTagsCollection
                                {
                                    { "settingKey.name", item.SettingKey.ToString() },
                                    { "settingKey.provider", item.SettingKey.GetProviderType() },
                                    { "settingKey.resourceType", item.SettingKey.GetResourceType() },
                                    { "settingKey.cacheEntryExist", cacheEntryExist },
                                    { "settingKey.cacheCount",
                                        typeof(ICollection).IsAssignableFrom(item.SettingKey.GetResourceType())
                                        ? (cacheEntry is ICollection collection ? collection.Count : 0)
                                        : cacheEntryExist ? 1 : 0 },
                                }));
                    }
                }
            }
        }

        /// <summary>
        /// GetSettings - get value by keys from AppSettings
        /// </summary>
        private IEnumerable<ConfigData> GetSettings(string providerType)
        {
            return GetSettings(null, providerType);
        }

        /// <summary>
        /// GetSettings - get value by keys from AppSettings
        /// </summary>
        private IEnumerable<ConfigData> GetSettings(IEnumerable<string>? settingKeys, string? providerType = null)
        {
            var results = DataSections
                .Where(x => settingKeys == null || settingKeys.Contains(x.Name));

            if (providerType != null)
            {
                results = results
                .Where(x => providerType == ProviderTypes.All
                    || providerType == ProviderTypes.AllPreload
                    || (x.Provider != null && x.Provider.StartsWith(providerType)));
            }

            return results; //.ToDataList();
        }

        /// <summary>
        /// GetSettings - get value by keys from AppSettings
        /// </summary>
        private IEnumerable<ConfigData> GetSettings(IEnumerable<string>? settingKeys)
        {
            return DataSections.Where(x => settingKeys == null || settingKeys.Contains(x.Name));
        }

        #endregion Public methods

        #region Private methods

        /// <summary>
        /// LoadFromOptima - load value from Optima
        /// </summary>
        public async Task<ConfigData> LoadFromOptima(ConfigData sectionData)
        {
            var result = await staticDataRepository.ReadAsync<object>(sectionData.SettingKey, CancellationToken.None);

            if (!result.IsSuccess)
                logger.Error("{TITLE} error: {MESSAGE}", ApiHelper.LogTitle(), result.Message);

            sectionData.Data = result.IsSuccess
                ? result.Data : default;

            //if (!ApiHelper.IsDataNullOrEmpty(x.Data))
            //{
            //    var configurationProvider = GetService<IConfigurationProvider>(x.SettingKey);
            //    var provider = (ApiConfigurationProvider)configurationProvider;
            //    await provider.SetAsync(x.SettingKey, x.Data);
            //}

            return sectionData;
        }

        /// <summary>
        /// LoadCachedStaticData - get value from MemoryCache or RedisCache
        /// </summary>
        private async Task LoadCachedStaticData(IEnumerable<string>? settingKeys = null, int ttl = TTL.TenMinutes)
        {
            if (!apiSettings.Optima.UseMemoryCache)
            {
                logger.Information("{TITLE} warning: UseMemoryCache is false", ApiHelper.LogTitle());
                return;
            }

            try
            {
                var tasks = DataContext
                    .Where(x => settingKeys == null || settingKeys.Contains(x.Name))
                    .Select(async data =>
                    {
                        object? redisValue = default;
                        try
                        {
                            var redisCacheKey = $"{CacheKeys.StaticData}_{apiSettings.Version}:{data.SettingKey}";
                            redisValue = await memoryCache.GetOrCreateAsync(data.SettingKey, async entry =>
                            {
                                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttl);
                                var resourceType = data.SettingKey.GetResourceType();
                                return await blobStorage.DownloadAsync(apiSettings.Version, redisCacheKey, resourceType);
                            });
                        }
                        catch (Exception ex)
                        {
                            logger.Error("{TITLE} exception: {MESSAGE}. \nStackTrace: {STACKTRACE}\n",
                                ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                        }

                        data.Error = ApiHelper.IsDataNullOrEmpty(redisValue);
                        data.Message = redisValue != null ? string.Empty : $"Redis {data.SettingKey} not found";
                        data.Data = JsonConvert.SerializeObject(redisValue);
                        data.Value = redisValue;
                    });

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                logger.Error("{TITLE} exception: {MESSAGE}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
        }

        private void LoadAppSettingStaticData()
        {
            var settings = DataContext.Where(x => ApiHelper.IsDataNullOrEmpty(x.Value) && x.Provider == ProviderTypes.StaticList);

            if (settings.Any())
            {
                try
                {
                    foreach (var x in settings)
                    {
                        var dataItems = x.Data?.ToString()?.Split(",").Select(item => item.Trim()).ToList() ?? [];
                        object? dataValue = dataItems.All(ApiHelper.IsDigitsOnly)
                            ? dataItems.Select(int.Parse).ToList() : dataItems;

                        x.Value = dataValue;
                        x.Error = ApiHelper.IsDataNullOrEmpty(dataValue);
                        x.Data = x.Data;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("{TITLE} exception: {MESSAGE}. \nStackTrace: {STACKTRACE}\n",
                        ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                }
            }
        }

        /// <summary>
        /// LoadExternalStaticData - get value by keys from External StaticDataService
        /// </summary>
        private async Task LoadExternalStaticData(string providerType, int ttl = TTL.TenMinutes)
        {
            var undefinedSettings = providerType == ProviderTypes.AllPreload
              ? DataContext.Select(x => x.Name)
              : DataContext.Where(x => ApiHelper.IsDataNullOrEmpty(x.Value)).Select(x => x.Name);

            List<EntityData> staticDataResults = [];

            if (undefinedSettings.Any())
            {
                try
                {
                    IServiceResult<List<EntityData>>? staticDataResult = default;
                    var redisResult = await GetFromRedis(appSettingsOptions.Value.Version, undefinedSettings);

                    if (!redisResult.IsSuccess || redisResult?.Value?.Count != DataSections.Count)
                    {
                        logger.Warning("{TITLE} StaticData not exists in redis.", ApiHelper.LogTitle());

                        // LoadExternalStaticData-step-01 get value from StaticList
                        var optimaStaticDataApi = apiHttpClientFactory.CreateClient(ApiHttpClientNames.OptimaStaticDataApi);
                        staticDataResult = await optimaStaticDataApi.PostAsync<string[], List<EntityData>>(
                            $"/api/StaticData/GetFromBlobAsync?version={apiSettings.Version}", undefinedSettings.ToArray());

                        if (!staticDataResult.IsSuccess)
                        {
                            logger.Error("{TITLE} optimaStaticDataApi is failed. Url: {URL}. Error: {MESSAGE}",
                                ApiHelper.LogTitle(),
                                $"{optimaStaticDataApi.BaseAddress}/api/StaticData/GetFromBlobAsync?version={apiSettings.Version}",
                                staticDataResult.Message);

                            // LoadExternalStaticData-step-02 get from Blob if optimaStaticDataApi is failed
                            var blobResult = await GetFromBlobAsync(appSettingsOptions.Value.Version);
                            staticDataResult = blobResult;
                        }
                    }

                    // LoadExternalStaticData-step-03 set value to MemoryCache
                    staticDataResult?.Value?.ForEach(item =>
                    {
                        if (Enum.TryParse(item.Name, out SettingKeys settingKey))
                        {
                            var dataItem = DataContext.FirstOrDefault(x => x.Name == item.Name);
                            if (dataItem != null)
                            {
                                dataItem.SettingKey = settingKey;
                                dataItem.Data = item.Value;
                                var resourceType = dataItem.SettingKey.GetResourceType();

                                dataItem.Value = dataItem.Data switch
                                {
                                    string data when ApiJsonHelper.IsValidAndNotEmpty<string>(data) => JsonConvert.DeserializeObject(data, resourceType),
                                    JArray jdata => jdata.ToObject(resourceType),
                                    JObject jobjdata => jobjdata.ToObject(resourceType),
                                    _ => dataItem.Data
                                };

                                dataItem.Error = ApiHelper.IsDataNullOrEmpty(dataItem.Value);
                                //memoryCache.Set(dataItem.SettingKey, dataItem.Value, TimeSpan.FromSeconds(ttl));
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    logger.Error("{TITLE} Currency rates response exeption: {ERROR}. \nStackTrace: {STACKTRACE}\n",
                        ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                }
            }
        }

        private async Task PreloadExternalStaticData(string providerType, int ttl = TTL.TenMinutes)
        {
            var undefinedSettings = providerType == ProviderTypes.AllPreload
              ? DataContext.Select(x => x.Name).ToList()
              : DataContext.Where(x => ApiHelper.IsDataNullOrEmpty(x.Value)).Select(x => x.Name).ToList();

            if (undefinedSettings.Any())
            {
                try
                {
                    List<EntityData> staticDataResults = [];
                    var tasks = new List<Task<IServiceResult<List<EntityData>>>>();

                    if (providerType.StartsWith(ProviderTypes.All) || providerType.StartsWith(ProviderTypes.StaticList))
                        tasks.Add(Task.Run(() => Task.FromResult(GetFromStaticList(undefinedSettings))));

                    if (providerType.StartsWith(ProviderTypes.All) || providerType.StartsWith(ProviderTypes.Optima))
                        tasks.Add(GetFromOptima(undefinedSettings));

                    if (providerType.StartsWith(ProviderTypes.All) || providerType.StartsWith(ProviderTypes.Umbraco))
                        tasks.Add(GetFromUmbraco(undefinedSettings));

                    if (providerType.StartsWith(ProviderTypes.All) || providerType.StartsWith(ProviderTypes.Nop))
                        tasks.Add(GetFromNop());

                    // TODO: temporary blocked
                    // if (providerType.StartsWith(ProviderTypes.All) || providerType.StartsWith(ProviderTypes.Dal))
                    //     tasks.Add(GetCurrencyRatesAsync(settingKeys));

                    var results = await Task.WhenAll(tasks);

                    foreach (var result in results)
                    {
                        if (result.IsSuccess && result.Value != null)
                        {
                            foreach (var item in result.Value)
                            {
                                var dataItem = DataContext.FirstOrDefault(x => x.Name == item.Name);
                                if (dataItem != null && Enum.TryParse(item.Name, out SettingKeys settingKey))
                                {
                                    dataItem.SettingKey = settingKey;
                                    dataItem.Value = item.Value;
                                    dataItem.Error = ApiHelper.IsDataNullOrEmpty(dataItem.Value);
                                    //memoryCache.Set(dataItem.SettingKey, dataItem.Value, TimeSpan.FromSeconds(ttl));
                                }
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    logger.Error("{TITLE} Currency rates response exeption: {ERROR}. \nStackTrace: {STACKTRACE}\n",
                        ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                }
            }
        }

        #endregion
    }
}