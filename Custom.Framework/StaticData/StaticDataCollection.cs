using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Extensions;
using Custom.Framework.Helpers;
using Custom.Framework.Models.Base;
using Custom.Framework.StaticData.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Collections;

namespace Custom.Framework.StaticData
{
    /// <summary>
    /// StaticDataCollection - A collection of static data items that are cached in memory
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class StaticDataCollection<T> : ICollection<T> where T : EntityData
    {
        #region privates

        private List<T> _innerList = new();
        private readonly ILogger _logger;
        private readonly ApiSettings _apiSettings;
        private TimeSpan _cacheExpiration { get; set; }
        private IStaticDataService _parentStaticDataService;
        private static readonly MemoryCacheEntryOptions _cacheOptions = new() { Priority = CacheItemPriority.NeverRemove };

        #endregion privates

        #region props

        public IConfiguration Configuration { get; set; }
        public IMemoryCache MemoryCache { get; set; }

        #endregion props

        public StaticDataCollection(
            IMemoryCache memoryCache,
            ILogger logger, IConfiguration config,
            IOptions<ApiSettings> apiSettingsOptions,
            IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _apiSettings = apiSettingsOptions.Value;
            _cacheExpiration = TimeSpan.FromSeconds(_apiSettings.Optima.CacheMemoryReloadTTL);
            Configuration = config;
            MemoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        #region public methods

        public void SetParent(IStaticDataService parentService)
        {
            _parentStaticDataService = parentService;
        }

        // Add an item to the list and cache it
        public void Add(T item)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(item);

                var cacheKey = GenerateCacheKey(item.Name);
                var index = _innerList.FindIndex(x => x.SettingKey == item.SettingKey);

                if (index >= 0)
                    _innerList[index] = item;
                else
                    _innerList.Add(item);

                MemoryCache.Set(cacheKey, item, _cacheOptions);
            }
            catch (Exception ex)
            {
                _logger.Error("{Title} error: {Message} on add the setting key: {SettingKey}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, item.SettingKey, ex.StackTrace);
            }
        }

        /// <summary>
        /// Get value or default from the cache
        /// </summary>
        public T? GetValueOrDefault(int index)
        {
            if (index < 0 || index >= _innerList.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            var item = _innerList[index];
            var cacheKey = GenerateCacheKey(item.Name);

            if (MemoryCache.TryGetValue(cacheKey, out T? cachedItem))
            {
                return cachedItem; // Return cached item if available
            }

            if (item.Provider == ProviderTypes.StaticList)
            {
                var serviceResult = _parentStaticDataService.GetFromStaticList([item.Name]); // Load from StaticList
                var result = serviceResult.Value;
                var dataItem = result?.FirstOrDefault() as T;
                if (dataItem != null)
                    _innerList[index] = dataItem;
            }
            else
            {
                Enum.TryParse<SettingKeys>(item.Name, out var settingKey);
                var serviceResult = _parentStaticDataService.GetFromBlobAsync(_apiSettings.Version, [item.Name]); // Load from Blob)
                var result = serviceResult.Result.Value;
                var dataItem = result?.FirstOrDefault() as T;
                if (dataItem != null)
                    _innerList[index] = dataItem;
            }

            if (item != null && item.Value != null)
            {
                //MemoryCache.Set(cacheKey, item, _cacheExpiration);
            }

            return item;
        }

        /// <summary>
        /// Get value or default from the cache
        /// </summary>
        public async Task<T?> GetValueOrDefaultAsync(int index)
        {
            try
            {
                if (index < 0 || index >= _innerList.Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                var item = _innerList[index];
                var cacheKey = GenerateCacheKey(item.Name);

                if (MemoryCache.TryGetValue(cacheKey, out T? cachedItem))
                {
                    return cachedItem; // Return cached item if available
                }

                List<EntityData>? result;
                if (item.Provider == ProviderTypes.StaticList)
                {
                    var serviceResult = _parentStaticDataService.GetFromStaticList([item.Name]);
                    result = serviceResult?.Value;
                }
                else
                {
                    Enum.TryParse<SettingKeys>(item.Name, out var settingKey);
                    var serviceResult = await _parentStaticDataService.GetFromBlobAsync(_apiSettings.Version, [item.Name]);
                    result = serviceResult?.Value;
                }

                var value = result?.FirstOrDefault();
                if (value != null)
                    _innerList[index] = (T)value;

                return item;

            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                return default;
            }
        }

        /// <summary>
        /// Get value or default from the cache
        /// </summary>
        public TData? GetValueOrDefault<TData>(SettingKeys settingKey)
        {
            var cacheKey = GenerateCacheKey(settingKey);
            if (MemoryCache.TryGetValue(cacheKey, out TData? cachedItem))
            {
                return cachedItem;
            }

            if (!_innerList.Any(x => x.Name == settingKey.ToString()))
                throw new ArgumentOutOfRangeException(settingKey.ToString());

            T? item = _innerList.FirstOrDefault(x => x.Name == settingKey.ToString());

            if (item == null)
                return typeof(TData).GetDefault<TData>();

            MemoryCache.Set(cacheKey, item, _cacheExpiration);

            return item.Value is TData data
                ? data
                : typeof(TData).GetDefault<TData>();

        }

        /// <summary>
        /// Remove from the cache
        /// </summary>
        public bool Remove(T item)
        {
            if (item == null) return false;

            var cacheKey = GenerateCacheKey(item.Name);
            MemoryCache.Remove(cacheKey); // Remove from cache
            return _innerList.Remove(item);
        }

        /// <summary>
        /// Clear cache
        /// </summary>
        public void Clear()
        {
            foreach (var item in _innerList)
            {
                MemoryCache.Remove(GenerateCacheKey(item.Name));
            }
            _innerList.Clear();
        }

        #region ICollection<T> Implementation

        public int Count => _innerList.Count;

        public bool IsReadOnly => false;

        public bool Contains(T item) => _innerList.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => _innerList.CopyTo(array, arrayIndex);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerator<T?> GetEnumerator()
        {
            for (int i = 0; i < _innerList.Count; i++)
            {
                yield return _innerList[i];
                //GetValueOrDefault(i); // Retrieve items through caching logic
            }
        }

        public async IAsyncEnumerable<T?> GetAsyncEnumerator()
        {
            for (int i = 0; i < _innerList.Count; i++)
            {
                var value = await GetValueOrDefaultAsync(i);
                yield return value;
            }
        }

        #endregion ICollection<T> Implementation

        /// <summary>
        /// Load all setting keys from the data source
        /// </summary>
        public async IAsyncEnumerable<T?> LoadAsync()
        {
            for (int i = 0; i < _innerList.Count; i++)
            {
                var value = await GetValueOrDefaultAsync(i);
                yield return value;
            }
        }

        /// <summary>
        /// Set all setting keys to memory cache
        /// </summary>
        public void SetMemoryCache(IEnumerable<string>? settingKeys = null)
        {
            foreach (var item in _innerList)
            {
                if (item != null && (settingKeys == null || settingKeys.Contains(item.Name)))
                {
                    MemoryCache.Set(item.Name, item, _cacheOptions);
                }
            }
        }

        /// <summary> Fetch data from source and reload the memory cache </summary>
        public async Task ReloadAsync(CancellationToken cancellationToken)
        {
            try
            {
                var dataItems = await FetchDataFromSourceAsync(cancellationToken);
                Interlocked.Exchange(ref _innerList, dataItems);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error reloading memory cache: {message}", ex.Message);
            }
        }

        #endregion public methods

        #region private methods

        /// <summary> Fetch data from the source </summary>
        private async Task<List<T>> FetchDataFromSourceAsync(CancellationToken cancellationToken)
        {
            // in cases of xunit _parentStaticDataService in arrange time may be null
            if (_parentStaticDataService == null || cancellationToken.IsCancellationRequested)
                return [];

            var blobData = await _parentStaticDataService.GetFromBlobAsync(_apiSettings.Version);
            var dataList = blobData.Value?.Cast<T>().ToList();

            return dataList ?? [];
        }

        /// <summary> Helper method to generate unique cache keys </summary>
        private string GenerateCacheKey(T item) => GenerateCacheKey(item.Name);
        private string GenerateCacheKey(string name) => $"{SettingKeys.StaticData}_{name}";
        private string GenerateCacheKey(SettingKeys settingKey) => $"{SettingKeys.StaticData}_{settingKey}";

        #endregion private methods
    }
}