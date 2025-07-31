using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Custom.Framework.Cache;
using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Configuration.Optima;
using Custom.Framework.Configuration.Umbraco;
using Custom.Framework.Contracts;
using Custom.Framework.Core;
using Custom.Framework.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;
using System.Text;

namespace Custom.Framework.StaticData.Services
{
    public class ApiSettingsService //: IApiSettingsService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IMemoryCache _memoryCache;
        private readonly IRedisCache _redisCache;
        private readonly ILogger _logger;
        private readonly ApiSettings _appSettings;
        private readonly IApiCacheOptions _apiMemoryCacheOptions;
        private readonly IHttpClientFactory _httpClientFactory;
        private MemoryCacheEntryOptions _memoryCacheEntryOptions;
        private TimeSpan _absoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);

        public ApiSettingsService(BlobServiceClient blobServiceClient,
            IMemoryCache memoryCache, IRedisCache redisCache, ILogger logger,
            IOptions<ApiSettings> appSettingsOptions, IHttpClientFactory httpClientFactory,
            IApiCacheOptions apiMemoryCacheOptions, MemoryCacheEntryOptions memoryCacheEntryOptions)
        {
            _logger = logger;
            _redisCache = redisCache;
            _memoryCache = memoryCache;
            _blobServiceClient = blobServiceClient;
            _appSettings = appSettingsOptions.Value;
            _apiMemoryCacheOptions = apiMemoryCacheOptions;
            _httpClientFactory = httpClientFactory;
            _memoryCacheEntryOptions = memoryCacheEntryOptions;
        }

        #region public methods

        /// <summary> Get settings by SettingKey </summary>
        public T? Get<T>(SettingKeys settingsKey)
        {
            // return results if settingsKey cacheEntry exist
            var cacheEntryExist = _memoryCache.TryGetValue(settingsKey, out T? cacheEntry);
            if (!cacheEntryExist)
            {
                var results = Execute<T>(settingsKey);
                if (results != null)
                {
                    var cacheEntryOptions = _apiMemoryCacheOptions.GetMemoryCacheOptions(settingsKey, 3600);
                    var cacheSet = _memoryCache.Set(settingsKey, results, cacheEntryOptions);

                    if (cacheSet == null)
                        _logger.Error("{TITLE} MemoryCache set failed: setvalue '{OBJECTNAME}' is null",
                            ApiHelper.LogTitle(), nameof(cacheSet));

                    return cacheSet;
                }
                else
                {
                    _logger.Error("{TITLE} failed: results is null", ApiHelper.LogTitle());
                    return default;
                }

            }
            else if (cacheEntryExist && cacheEntry != null)
            {
                return cacheEntry;
            }
            else
            {
                _logger.Error("{TITLE} failed", ApiHelper.LogTitle());
                return default;
            }
        }

        /// <summary> GetSettings settings by SettingKey </summary>
        public async Task<T?> GetAsync<T>(SettingKeys settingsKey)
        {
            // return results if settingsKey cacheEntry exist
            var cacheEntryExist = _memoryCache.TryGetValue(settingsKey, out T? cacheEntry);
            if (!cacheEntryExist)
            {
                var results = await GetFromBlobAsync<T>(settingsKey);
                if (results != null)
                {
                    var cacheEntryOptions = _apiMemoryCacheOptions.GetMemoryCacheOptions(settingsKey, 3600);
                    var cacheSet = _memoryCache.Set(settingsKey, results, cacheEntryOptions);

                    if (cacheSet == null)
                        _logger.Error("{TITLE} MemoryCache set failed: setvalue '{OBJECTNAME}' is null",
                            ApiHelper.LogTitle(), nameof(cacheSet));

                    return cacheSet;
                }
                else
                {
                    _logger.Error("{TITLE} failed: results is null", ApiHelper.LogTitle());
                    return default;
                }

            }
            else if (cacheEntryExist && cacheEntry != null)
            {
                return cacheEntry;
            }
            else
            {
                _logger.Error("{TITLE} failed", ApiHelper.LogTitle());
                return default;
            }
        }

        /// <summary> UploadDataToBlobAsync settings to blob </summary>
        public async Task<bool> SetAsync<T>(string blobName, T data)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_appSettings.AzureStorage.ContainerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            string content = JsonConvert.SerializeObject(data);
            byte[] bytes = Encoding.UTF8.GetBytes(content);

            using MemoryStream stream = new MemoryStream(bytes);
            await blobClient.UploadAsync(stream, true);

            return true;
        }

        public ApiSettings? GetAll(ApiSettings? _apiSettings)
        {
            if (_memoryCache.TryGetValue(SettingKeys.ApiSettings, out ApiSettings? cacheEntry))
            {
                _logger.Warning("{TITLE} ApiSettings completed from cache", ApiHelper.LogTitle());
                return _apiSettings = cacheEntry;
            }

            cacheEntry = _redisCache.Get<ApiSettings>(CacheKeys.ApiSettings.ToString());
            if (cacheEntry != null)
            {
                _logger.Warning("{TITLE} {SETTINGS} got from cache", ApiHelper.LogTitle(), SettingKeys.ApiSettings.ToString());
                return _apiSettings = cacheEntry;
            }

            _logger.Warning("{TITLE} Settings initialization start", ApiHelper.LogTitle());

            var results = ExecuteAllAsync().GetAwaiter().GetResult();
            if (results != null)
            {
                _memoryCacheEntryOptions = _apiMemoryCacheOptions.GetMemoryCacheOptions(SettingKeys.ApiSettings, 3600,
                    async (key, value, reason, state) => await OnPostEviction(key, value, reason, _apiSettings));

                var cacheSet = _memoryCache.Set(SettingKeys.ApiSettings, results, _memoryCacheEntryOptions);

                if (cacheSet == null)
                    _logger.Error("{TITLE} MemoryCache set failed", ApiHelper.LogTitle());
                else
                    _apiSettings = cacheEntry;

                var ttl = _apiMemoryCacheOptions.GetRedisCacheTtl(CacheKeys.ApiSettings);
                var isRedisSet = _redisCache.Set(CacheKeys.ApiSettings.ToString(), results, ttl);

                if (!isRedisSet)
                    _logger.Error("{TITLE} RedisCache set failed", ApiHelper.LogTitle());

                _logger.Warning("{TITLE} Initialization complete", ApiHelper.LogTitle());
                return cacheSet!;
            }
            else
            {
                _logger.Error("{TITLE} failed: Execute results is null", ApiHelper.LogTitle());
                _logger.Information("{STEPNAME} step. Initialization failed");
                return default;
            }
        }

        /// <summary> GetAllAsync into ApiSettings </summary>
        public async Task<ApiSettings?> GetAllAsync()
        {
            if (_memoryCache.TryGetValue(SettingKeys.ApiSettings, out ApiSettings? cacheEntry))
            {
                _logger.Warning("{STEPNAME} step. ApiSettings completed from cache");
                return cacheEntry;
            }

            cacheEntry = await _redisCache.GetAsync<ApiSettings>(CacheKeys.ApiSettings.ToString());
            if (cacheEntry != null)
            {
                _logger.Information("{STEPNAME} step. {SETTINGS} got from cache",
                    ApiHelper.LogTitle(), SettingKeys.ApiSettings.ToString());
                return cacheEntry;
            }

            _logger.Information("{TITLE} Settings initialization start", ApiHelper.LogTitle());

            var results = await ExecuteAllAsync();
            if (results != null)
            {
                var cacheSet = _memoryCache.Set(SettingKeys.ApiSettings, results, _memoryCacheEntryOptions);
                if (cacheSet == null)
                    _logger.Error("{TITLE} MemoryCache set failed", ApiHelper.LogTitle());

                var ttl = _apiMemoryCacheOptions.GetRedisCacheTtl(CacheKeys.ApiSettings);
                var isRedisSet = await _redisCache.SetAsync(CacheKeys.ApiSettings.ToString(), results, ttl);

                if (!isRedisSet)
                    _logger.Error("{TITLE} RedisCache set failed", ApiHelper.LogTitle());

                _logger.Information("{STEPNAME} step. Initialization complete");
                return cacheSet!;
            }
            else
            {
                _logger.Error("{TITLE} failed: Execute results is null", ApiHelper.LogTitle());
                _logger.Information("{STEPNAME} step. Initialization failed");
                return default;
            }
        }

        // Verify folder exists in azure blob container
        public Task<bool> BlobFolderExistsAsync(string? folderName = null)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_appSettings.AzureStorage.ContainerName);
                folderName ??= _appSettings.Version.ToUpper();

                // Use the list blobs operation to check if blobs with the specified prefix (folder name) exist.
                var blobs = containerClient.GetBlobs(prefix: folderName).ToList();
                if (blobs.Any())
                {
                    return Task.FromResult(true);
                }
                else
                {
                    _logger.Error("{TITLE} error: blob client {BLOB_CONTAINER_NAME} folder {FOLDER} not found ",
                        ApiHelper.LogTitle(), _appSettings.AzureStorage.ContainerName, folderName);

                    return Task.FromResult(false);
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.Error("{TITLE} error: ContainerName {BLOB_CONTAINER_NAME} not found. Title: {ERROR}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), _appSettings.AzureStorage.ContainerName,
                    ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                return Task.FromResult(false); // Container does not exist.
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} error: {BLOB} failed. Exception: {ERROR}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), _appSettings.AzureStorage.ContainerName,
                    ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                return Task.FromResult(false);
            }
        }

        #endregion public methods

        #region private methods

        private async Task ReloadAsync(object key, object? settings)
        {
            var settingsKey = key.ToString();
            settings = settingsKey switch
            {
                _ => await GetAllAsync(),
            };

            if (settings != null)
            {
                _memoryCacheEntryOptions = _apiMemoryCacheOptions.GetMemoryCacheOptions(CacheKeys.ApiSettings, 3600,
                    async (key, value, reason, state) => await OnPostEviction(key, value, reason, settings));
                _memoryCacheEntryOptions.AbsoluteExpirationRelativeToNow = _absoluteExpirationRelativeToNow;

                _memoryCache.Set(key, settings, _memoryCacheEntryOptions);
            }
        }

        /// <summary>
        /// OnPostEviction - callback which gets called when a cache entry expires.
        /// </summary>
        private async Task OnPostEviction(object key, object? value, EvictionReason reason, object? state)
        {
            if (reason == EvictionReason.Expired)
            {
                _logger.Warning("{TITLE OnPostEviction Message: Cache key {KEY} entry expired.", ApiHelper.LogTitle(), key);
            }
            else if (reason == EvictionReason.Removed)
            {
                _logger.Warning("{TITLE} OnPostEviction Message: Cache key {KEY} entry explicitly removed.", ApiHelper.LogTitle(), key);
            }
            else
            {
                _logger.Warning("{TITLE} OnPostEviction Message: Cache key {KEY} entry evicted.", ApiHelper.LogTitle(), key);
            }

            await ReloadAsync(key.ToString()!, state);
        }

        private T? Execute<T>(SettingKeys settingsKey)
        {
            var blobFolderName = _appSettings.Version.ToUpper();
            var blobName = $"{settingsKey}.json";

            try
            {
                var blobContainerClient = _blobServiceClient.GetBlobContainerClient(_appSettings.AzureStorage.ContainerName);
                var blobClient = blobContainerClient.GetBlobClient($"{blobFolderName}/{blobName}");

                if (blobClient.Exists())
                {
                    BlobDownloadInfo blobDownloadInfo = blobClient.Download();
                    using var reader = new StreamReader(blobDownloadInfo.Content);

                    var content = reader.ReadToEnd();
                    var data = ParseContent<T>(settingsKey, content);
                    return data;
                }
                else
                {
                    _logger.Error("{TITLE} error: blob client {CLIENT} folder {FOLDER} not found",
                        ApiHelper.LogTitle(), _appSettings.AzureStorage.ContainerName,
                        $"{blobFolderName}/{blobName}");
                }
            }
            catch (Exception e)
            {
                _logger.Error("{TITLE} error: get key {KEY} failed. Message: {ERROR}. Stack: {ST}",
                       ApiHelper.LogTitle(), settingsKey,
                       e.InnerException?.Message ?? e.Message, e.StackTrace);
            }
            return default;
        }

        private async Task<ApiSettings?> ExecuteAllAsync()
        {
            var t1 = GetAsync<Dictionary<int, UmbracoSettings>>(SettingKeys.UmbracoSettings);
            var t2 = GetAsync<OptimaSettings>(SettingKeys.OptimaSettings);
            //var t3 = GetSettings<List<Discount>>(SettingKeys.Sales);

            await Task.WhenAll(t1, t2);

            return new ApiSettings
            {
                Version = _appSettings.Version,
                //UmbracoSettings = t1.Result
                //    ?? throw new ApiException(ServiceStatus.FatalError, t1.Result?.GetType().FilterName!),
                //OptimaUmbracoSettings = t2.Result
                //    ?? throw new ApiException(ServiceStatus.FatalError, t2.Result?.GetType().FilterName!)
            };
        }

        private async Task<T?> GetFromBlobAsync<T>(SettingKeys settingsKey)
        {
            try
            {
                var blobContainerClient = _blobServiceClient.GetBlobContainerClient(_appSettings.AzureStorage.ContainerName);
                var blobFolderName = _appSettings.Version.ToUpper();
                var blobName = $"{settingsKey}.json";
                var blobClient = blobContainerClient.GetBlobClient($"{blobFolderName}/{blobName}");

                if (await blobClient.ExistsAsync())
                {
                    BlobDownloadInfo blobDownloadInfo = await blobClient.DownloadAsync();
                    using var reader = new StreamReader(blobDownloadInfo.Content);

                    var content = await reader.ReadToEndAsync();
                    var data = ParseContent<T>(settingsKey, content);
                    return data;
                }
                else
                {
                    _logger.Error("{TITLE} error: blob client {CLIENT} folder {FOLDER} not found ",
                        ApiHelper.LogTitle(), _appSettings.AzureStorage.ContainerName,
                        $"{blobFolderName}/{blobName}");
                }
            }
            catch (Exception e)
            {
                _logger.Error("{TITLE} error: get key {KEY} failed. Message: {ERROR}. Stack: {ST}",
                       ApiHelper.LogTitle(), settingsKey,
                       e.InnerException?.Message ?? e.Message, e.StackTrace);
            }
            return default;
        }

        private async Task<T?> GetFromDbAsync<T>(CancellationToken cancellationToken = default)
        {
            var dalUrl = _appSettings.Dal.Host;
            var dalPath = _appSettings.Currency?.CurrencyRatesUrl;

            try
            {
                using var httpClient = _httpClientFactory.CreateClient(ApiHttpClientNames.DalApi);
                var response = await httpClient.GetAsync(dalPath, cancellationToken);

                T? data = default;
                if (response.IsSuccessStatusCode)
                {
                    string res = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(res))
                    {
                        data = JsonConvert.DeserializeObject<T>(res);
                    }
                }

                _logger.Error("{TITLE} Currency rates response content is empty.", ApiHelper.LogTitle());
                return data;
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} Currency rates response exeption: {EX}", ApiHelper.LogTitle(), ex);
                return default;
            }
        }

        private T? ParseContent<T>(SettingKeys settingsKey, string content)
        {
            // Get the last generic argument, which is the value type
            Type? valueType = typeof(T?).GetGenericArguments().Length != 0
                ? typeof(T?).GetGenericArguments().LastOrDefault()
                : default;

            switch (settingsKey)
            {
                case SettingKeys.UmbracoSettings:
                    var allSitesSettings = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
                    if (allSitesSettings != null)
                    {
                        var umbracoSearchSettingsData = allSitesSettings.ToDictionary(
                            site => int.Parse(site.Key),
                            site => JsonConvert.DeserializeObject<UmbracoSettings>(site.Value));

                        return (T?)Convert.ChangeType(umbracoSearchSettingsData, typeof(T));
                    }
                    else
                    {
                        _logger.Error("{TITLE} error: {KEY} parsing failed", ApiHelper.LogTitle(), settingsKey);
                        throw new NotImplementedException($"{settingsKey} parsing failed");
                    }
                case SettingKeys.OptimaSettings:
                    var allSitesOptimaSettings = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
                    if (allSitesOptimaSettings != null)
                    {
                        var umbracoSearchSettingsData = allSitesOptimaSettings.ToDictionary(
                            site => int.Parse(site.Key),
                            site => JsonConvert.DeserializeObject<OptimaSettings>(site.Value));

                        return (T?)Convert.ChangeType(umbracoSearchSettingsData, typeof(T?));
                    }
                    else
                    {
                        _logger.Error("{TITLE} error: {KEY} parsing failed", ApiHelper.LogTitle(), settingsKey);
                        throw new NotImplementedException($"{settingsKey} parsing failed");
                    }
                case SettingKeys.RoomSpecialRequestsCodes: // TODO: Need to imlement all SettingKeys
                case SettingKeys.NopSearchSettings:
                case SettingKeys.AllRatePlanCodes:
                case SettingKeys.DynamicFlightSettings:
                case SettingKeys.Sales:
                case SettingKeys.SalesPromotion:
                case SettingKeys.ReservationSettings:
                case SettingKeys.SunClubSettings:
                case SettingKeys.CancelReservationBccAddresses:
                case SettingKeys.OfflinePricesForAllRooms:
                case SettingKeys.ReservationTerms:
                case SettingKeys.ConnectingDoorReservationEmailAddresses:
                default:
                    _logger.Error("{TITLE} error: {KEY} parsing not implemented", ApiHelper.LogTitle(), settingsKey);
                    throw new NotImplementedException($"{settingsKey} parsing not implemented");
            }
        }

        private Task RefreshConfiguration(object state)
        {
            // Call the external API to check for updates
            //var updatedSettings = await GetSettings<UmbracoSettings>(EApiSettings.UmbracoSettings);
            //ar apiSettings = (new ApiSettings()).UmbracoSearch = updatedSettings;
            // Set the configuration if changes are detected
            //if (ConfigurationChanged(apiSettings, _apiSettingsMonitor.CurrentValue))
            //{
            //    _apiSettingsMonitor.OnChange(updatedSettings);
            //}
            return Task.CompletedTask;
        }

        private bool ConfigurationChanged(ApiSettings newSettings, ApiSettings currentSettings)
        {
            // Implement your logic to compare the old and new settings
            // Return true if changes are detected, otherwise false
            // You may need to customize this logic based on your specific use case
            return !newSettings.Equals(currentSettings);
        }

        #endregion private methods
    }

    //public class ApiSettingsFactory
    //{
    //    public IApiSettingsManager<TFilterType> CreateApiSettingsManager<TFilterType>(string apiName)
    //    {
    //        switch (apiName)
    //        {
    //            case "Api1":
    //                return new UmbracoSettingsManager<TFilterType>();
    //            default:
    //                throw new ArgumentException("Unsupported API");
    //        }
    //    }
    //}
}