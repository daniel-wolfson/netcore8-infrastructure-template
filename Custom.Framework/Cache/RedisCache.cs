using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Exceptions;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Custom.Framework.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Polly;
using Serilog;
using StackExchange.Redis;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;

namespace Custom.Framework.Cache
{
    public class RedisCache : ApiWorkerBase, IRedisCache, IRedisCacheFlush
    {
        #region fields
        private static readonly object _lock = new();
        private readonly ILogger _logger;
        private readonly ApiSettings _appSettings;
        private readonly CacheKeys _staticDataKey;
        private readonly ConfigurationOptions _configurationOptions;
        private readonly TimeSpan _lockTimeoutDefault = TimeSpan.FromSeconds(3);
        private int _initTimeCounter;
        private string _initTimeStamp = string.Empty;
        private IDatabase? _cacheDb = null;
        private IConnectionMultiplexer? _connectionMultiplexer;
        #endregion fields

        public RedisCache(
            IConnectionMultiplexer connection,
            ConfigurationOptions configurationOptions,
            IOptions<ApiSettings> appsettings,
            ILogger logger)
            : base()
        {
            _logger = logger;
            _configurationOptions = configurationOptions;
            _connectionMultiplexer = connection;
            _appSettings = appsettings.Value;
            _staticDataKey = _appSettings.Redis.StaticDataRootKey;
        }

        #region public props 

        public string? this[string key]
        {
            get
            {
                if (key.Contains(':'))
                {
                    var cacheKey = key.Split(':')[0] ?? $"{CacheKeys.StaticData}_{_appSettings.Version}";
                    var fieldKey = key.Split(':')[1];
                    var data = HashGet<string>(cacheKey, fieldKey).ToString();
                    return data;
                }
                else
                {
                    var data = Get(key);
                    return data;
                }
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                    _logger.Warning("{TITLE} Setting empty string is not allowed.", nameof(ApiConfigurationProvider));
                else
                    HashSet($"{CacheKeys.StaticData}_{_appSettings.Version}", key, value!, TTL.OneHour);
            }
        }

        // current Redis Db
        public IDatabase Database => _cacheDb ??= Connection.GetDatabase();
        // last Init TimeStamp, it is time last reconnect
        public string ReconnectTimeStamp => _initTimeStamp;
        public CacheKeys StaticData => _staticDataKey;
        public string RedisName { get; set; } = string.Empty;

        // current redis connection
        public IConnectionMultiplexer Connection
        {
            get
            {
                if (_connectionMultiplexer == null)
                    Reconnect();
                return _connectionMultiplexer ?? throw new ApiException(ServiceStatus.Conflict);
            }
            set
            {
                _connectionMultiplexer = value;
            }
        }

        #endregion public props

        #region connection

        private static readonly ApiTimeoutLock asyncLock = new();

        // ReconnectAsync, where isConnected = false throw RedisConnectionException
        public async Task<bool> ReconnectAsync(TimeSpan? timeout = null)
        {
            try
            {
                return await localReconnectAsync(timeout);
            }
            catch (Exception)
            {
                var isConnected = await RetryAsync<bool>(() => localReconnectAsync(timeout));
                if (!isConnected)
                    throw new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                        $"Failed connect to {_appSettings.Redis.ConnectionString}");
                return isConnected;
            }

            async Task<bool> localReconnectAsync(TimeSpan? timeout)
            {
                bool isConnected = false;
                var locker = await asyncLock.LockAsync(timeout);
                if (locker != null)
                {
                    _logger.Information("{TITLE} Connecting to {RedisClient}", ApiHelper.LogTitle(), _connectionMultiplexer?.ClientName);
                    _connectionMultiplexer = ConnectionMultiplexer.Connect(_configurationOptions);
                    _cacheDb = _connectionMultiplexer.GetDatabase();
                    _initTimeStamp = $"{Interlocked.Increment(ref _initTimeCounter)}_{DateTime.UtcNow:dd-MM-yyy_hh-mm-ss}";
                    isConnected = _connectionMultiplexer?.IsConnected ?? false;
                    asyncLock.Release();
                }
                else
                {
                    _logger.Warning("{TITLE} locked by previous reconnect", ApiHelper.LogTitle());
                }
                return isConnected;
            }
        }

        public bool Reconnect(TimeSpan? timeout = null)
        {
            try
            {
                return localReconnect();
            }
            catch (Exception)
            {
                var isConnected = Retry<bool>(() => localReconnect(timeout));
                if (!isConnected)
                    throw new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                        $"Failed connect to {_appSettings.Redis.ConnectionString}");
                return isConnected;
            }

            bool localReconnect(TimeSpan? timeSpan = null)
            {
                asyncLock.Lock(_lock, () =>
                {
                    _logger.Debug("{TITLE} {RedisClient} Connecting to {CLIENTNAME}...", ApiHelper.LogTitle(), _connectionMultiplexer?.ClientName);
                    _cacheDb = null;
                    _connectionMultiplexer = ConnectionMultiplexer.Connect(_configurationOptions);
                    _cacheDb = _connectionMultiplexer.GetDatabase();
                    _initTimeStamp = $"{Interlocked.Increment(ref _initTimeCounter)}_{DateTime.UtcNow:dd-MM-yyy_hh-mm-ss}";
                    var isConnected = _connectionMultiplexer?.IsConnected ?? false;
                    if (!isConnected) 
                        _logger.Warning("{TITLE} {RedisClient} Connection state: failed. TimeStamp: {ReconnectTimeStamp}",
                            ApiHelper.LogTitle(), _connectionMultiplexer?.ClientName, _initTimeStamp);
                }, _lockTimeoutDefault);

                return _connectionMultiplexer?.IsConnected ?? false;
            }
        }

        public bool IsConnected()
        {
            return Connection?.IsConnected ?? false;
        }

        private void UpdateConnection(RedisConfig options)
        {
            // Set the connection
            var newConnection = ConnectionMultiplexer.Connect(options.ConnectionString);

            // Dispose the old connection (if applicable)
            _connectionMultiplexer?.Dispose();

            // Set the reference to the new connection
            _connectionMultiplexer = newConnection;
        }

        #endregion connection

        #region public Key sync methods

        /// <summary> Get key value </summary>
        public string? Get(string key)
        {
            string? result = default;
            try
            {
                result = Database.StringGet(key);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                result = Retry(() =>
                {
                    var redisValue = Database.StringGet(key);
                    return redisValue;
                });
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        /// <summary> Get key value </summary>
        public object? Get(string key, Type? resourseType = null)
        {
            object? result = default;
            try
            {
                var redisValue = Database.StringGet(key);
                if (resourseType != null)
                {
                    result = resourseType == typeof(string)
                        ? redisValue.ToString()
                        : JsonConvert.DeserializeObject(redisValue.ToString(), resourseType);
                }
                else
                {
                    result = JsonConvert.DeserializeObject(redisValue.ToString()!);
                }
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                result = Retry(() =>
                {
                    var redisValue = Database.StringGet(key);
                    return redisValue;
                });
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        /// <summary> Get key value </summary>
        public T? Get<T>(string key)
        {
            T? result = default;
            try
            {
                var redisValue = Database.StringGet(key);
                if (redisValue.HasValue)
                {
                    var stringResult = Encoding.UTF8.GetString(redisValue!);
                    result = typeof(T) == typeof(string)
                        ? (T?)(object)stringResult.ToString()
                        : JsonConvert.DeserializeObject<T?>(stringResult!);
                }
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                result = Retry(() =>
                {
                    var redisValue = Database.StringGet(key);
                    return redisValue.HasValue ? JsonConvert.DeserializeObject<T?>(redisValue!) : default;
                });
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        /// <summary> Set key value </summary>
        public bool Set<T>(string key, T value, int cacheTime)
        {
            bool result = false;
            if (value == null) return result;

            try
            {
                cacheTime += 1;
                var cacheValue = typeof(T) == typeof(string)
                    ? value.ToString()! : JsonConvert.SerializeObject(value);
                var encodedValue = Encoding.UTF8.GetBytes(cacheValue);
                ArgumentNullException.ThrowIfNull(value);
                result = Database.StringSet(key, encodedValue, TimeSpan.FromSeconds(cacheTime));
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                result = Retry(() => Database.StringSet(key, JsonConvert.SerializeObject(value), TimeSpan.FromSeconds(cacheTime)));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        /// <summary>
        /// Returns if key exists.
        /// </summary>
        public bool Exists(string key)
        {
            bool result = false;
            try
            {
                result = Database.KeyExists(key);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                result = Retry(() => Database.KeyExists(key));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

            }
            return result;
        }

        /// <summary>
        /// Removes the specified key. A key is ignored if it does not exist.
        /// If UNLINK is available (Redis 4.0+), it will be used.
        /// </summary>
        public bool Remove(string key)
        {
            bool result = false;
            try
            {
                result = Database.KeyDelete(key);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                result = Retry(() => Database.KeyDelete(key));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        /// <summary>
        /// Removes the specified key. A key is ignored if it does not exist.
        /// </summary>
        public int RemoveByPattern(string pattern)
        {
            int result = 0;
            try
            {
                result = ExecuteRemoveByPattern(pattern);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                result = Retry(() => ExecuteRemoveByPattern(pattern));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        /// <summary>
        /// Delete all the keys of the database.
        /// </summary>
        public void FlushDb()
        {
            try
            {
                foreach (EndPoint ep in Connection.GetEndPoints())
                {
                    var server = Connection.GetServer(ep);
                    server.FlushDatabase();
                }
                _initTimeStamp = $"{Interlocked.Increment(ref _initTimeCounter)}_{DateTime.UtcNow:dd-MM-yyy hh-mm-ss}";
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
        }

        [Obsolete("it is not task async-await implementation")]
        public T? GetOrCreate<T>(string key, Func<T> actionToGetData, int ttl, bool isUseRedis)
        {
            var cache = Get<T>(key);
            if (cache != null && isUseRedis)
                return cache;

            T data = actionToGetData();

            if (isUseRedis)
                Set(key, data, ttl);

            return data;
        }

        [Display(Description = "Not use in PROD, it may be to work more slowly")]
        public Dictionary<string, T?> GetAll<T>(string pattern)
        {
            Dictionary<string, T?>? results = [];

            try
            {
                results = LocalGetAll(pattern);
            }
            catch (Exception ex) when (ex is RedisConnectionException || ex is RedisTimeoutException)
            {
                if (ex is RedisConnectionException)
                    Reconnect();

                results = Retry(() => LocalGetAll(pattern));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            return results ?? [];

            Dictionary<string, T?> LocalGetAll(string pattern)
            {
                var endPoints = Database.Multiplexer.GetEndPoints();
                var server = Database.Multiplexer.GetServer(endPoints.First());

                var keys = GetKeysByPatternAsync(Database, pattern).GetAwaiter().GetResult();

                //var keys = server.Keys(pattern: pattern)
                //    .Where(x => System.Enum.IsDefined(typeof(SettingKeys), x.ToString().Split(":").LastOrDefault() ?? ""))
                //    .ToArray();

                var data = keys.ToDictionary(
                    k => (SettingKeys)System.Enum.Parse(typeof(SettingKeys), k.ToString().Split(":").LastOrDefault() ?? ""),
                    v => Database.StringGet(v)
                );

                return data.ToDictionary(
                    k => k.Key.ToString(),
                    v =>
                    {
                        try
                        {
                            var result = v.Value.ToString();
                            if (!v.Value.HasValue)
                                return default;
                            var value = typeof(T) != typeof(string)
                                ? (T?)JsonConvert.DeserializeObject(result, v.Key.GetResourceType())
                                : (T?)(object?)result;
                            return value;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                                ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                            return default;
                        }
                    }
                );
            }
        }

        public async Task<IEnumerable<RedisKey>> GetKeysByPatternAsync(IDatabase db, string pattern)
        {
            var server = ((ConnectionMultiplexer)db.Multiplexer).GetServer(db.Multiplexer.GetEndPoints().First());
            var keys = new List<RedisKey>();

            // Use IAsyncEnumerable to iterate over the keys
            await foreach (var key in server.KeysAsync(pattern: pattern))
            {
                keys.Add(key);
            }

            return keys;
        }

        #endregion public Key sync methods

        #region public Key async methods

        /// <summary> Get key value </summary>
        public async Task<T?> GetAsync<T>(string key)
        {
            T? result = default;
            try
            {
                var redisValue = await Database.StringGetAsync(key);
                if (redisValue.HasValue)
                {
                    var value = Encoding.UTF8.GetString(redisValue!);

                    result = typeof(T) == typeof(string)
                        ? (T)(object)value.ToString()
                        : JsonConvert.DeserializeObject<T?>(value!);
                }
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                var value = await RetryAsync(() => Database.StringGetAsync(key));

                result = typeof(T) == typeof(string)
                    ? (T)(object)value.ToString()
                    : JsonConvert.DeserializeObject<T?>(value.ToString()!);
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        /// <summary> Get key value </summary>
        public async Task<object?> GetAsync(string key, Type? resourceType = null)
        {
            object? result = default;
            try
            {
                var redisValue = await Database.StringGetAsync(key);
                if (redisValue.HasValue)
                {
                    var stringResult = Encoding.UTF8.GetString(redisValue!);
                    result = redisValue.HasValue 
                        ? JsonConvert.DeserializeObject(stringResult!, resourceType!) 
                        : default;
                }
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                var redisValue = await RetryAsync(() => Database.StringGetAsync(key));
                result = redisValue.HasValue ? JsonConvert.DeserializeObject(redisValue!, resourceType!) : default;
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        [Display(Description = "Warning!!! in PROD, it may be to work more slowly")]
        public async Task<Dictionary<string, T?>> GetAllAsync<T>(string pattern)
        {
            Dictionary<string, T?>? results = [];

            try
            {
                results = await LocalGetAll(pattern);
            }
            catch (Exception ex) when (ex is RedisConnectionException || ex is RedisTimeoutException)
            {
                if (ex is RedisConnectionException)
                    await ReconnectAsync();

                results = await RetryAsync(() => LocalGetAll(pattern));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            return results ?? [];

            async Task<Dictionary<string, T?>> LocalGetAll(string pattern)
            {
                var endPoints = Database.Multiplexer.GetEndPoints();
                var server = Database.Multiplexer.GetServer(endPoints.First());

                var keys = server.Keys(pattern: pattern)
                    .Where(x => System.Enum.IsDefined(typeof(SettingKeys), x.ToString().Split(":").LastOrDefault() ?? ""))
                    .ToArray();

                var tasks = keys.ToDictionary(
                    k => (SettingKeys)System.Enum.Parse(typeof(SettingKeys), k.ToString().Split(":").LastOrDefault() ?? ""),
                    v => Database.StringGetAsync(v)
                );

                await Task.WhenAll(tasks.Values);

                return tasks.ToDictionary(
                    k => k.Key.ToString(),
                    v =>
                    {
                        try
                        {
                            var result = v.Value.Result.ToString().Trim('"');
                            var jsonString = System.Text.RegularExpressions.Regex.Unescape(result);
                            var jsonObj = JsonConvert.DeserializeObject(jsonString, v.Key.GetResourceType());
                            if (!v.Value.Result.HasValue)
                                return default;
                            var value = typeof(T) != typeof(string)
                                ? (T?)JsonConvert.DeserializeObject(jsonString, v.Key.GetResourceType())
                                : (T?)(object?)jsonString;
                            return value;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                                ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                            return default;
                        }
                    }
                );
            }
        }

        /// <summary> Set key value </summary>
        public async Task<bool> SetAsync<T>(string key, T value, int cacheTime)
        {
            bool result = false;
            if (value == null) return result;

            try
            {
                cacheTime += 1;
                var cacheValue = typeof(T) == typeof(string)
                        ? value.ToString()! : JsonConvert.SerializeObject(value);
                var encodedValue = Encoding.UTF8.GetBytes(cacheValue);
                ArgumentNullException.ThrowIfNull(value);
                result = await Database.StringSetAsync(key, encodedValue, TimeSpan.FromSeconds(cacheTime));
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                result = await RetryAsync(() => Database.StringSetAsync(key, JsonConvert.SerializeObject(value), TimeSpan.FromSeconds(cacheTime)));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            if (!result)
                _logger.Warning("{TITLE} not successful", ApiHelper.LogTitle());

            return result;
        }

        /// <summary> Remove key </summary>
        public async Task<bool> RemoveAsync(string key)
        {
            bool result = false;
            try
            {
                result = await Database.KeyDeleteAsync(key);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                result = await RetryAsync(() => Database.KeyDeleteAsync(key));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        /// <summary> Remove key by Pattern </summary>
        public async Task<int> RemoveByPatternAsync(string pattern)
        {
            int result = 0;
            try
            {
                result = await ExecuteRemoveByPatternAsync(pattern, result);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                result = await RetryAsync<int>(() => ExecuteRemoveByPatternAsync(pattern, result));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        public async Task<bool> ExistsAsync(string[] keys)
        {
            foreach (var key in keys)
            {
                if (await ExistsAsync(key))
                {
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> ExistsAsync(string key)
        {
            bool result = false;
            try
            {
                result = await Database.KeyExistsAsync(key);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                result = await RetryAsync<bool>(() => Database.KeyExistsAsync(key));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        public long StringIncrement(string key)
        {
            long result = 0;
            try
            {
                result = Database.StringIncrement(key);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                result = Retry(() => Database.StringIncrement(key));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        #endregion public key methods

        #region public Hash methods

        /// <summary>
        /// Returns the value associated with field in the hash stored at key.
        /// </summary>
        /// <param name="key">The key of the hash.</param>
        /// <param name="hashField">The field in the hash to get.</param>
        public bool HashSet(string hashKey, string cacheKey, string value, int cacheTime)
        {
            cacheTime += 1;
            var hashEntry = new HashEntry(new RedisValue(cacheKey), value);
            return HashSet(hashKey, cacheKey, hashEntry, cacheTime);
        }

        public T? HashGet<T>(string hashKey, string hashField)
        {
            T? result = default;
            try
            {
                var redisValue = Database.HashGet(hashKey, hashField);
                return GetValueOrDefault<T>(hashField, redisValue);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                var redisValue = Retry(() => Database.HashGet(hashKey, hashField));
                return GetValueOrDefault<T>(hashField, redisValue);
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        public string? HashGet(string hashKey, string hashField)
        {
            string? result = default;
            try
            {
                var redisValue = Database.HashGet(hashKey, hashField);
                return redisValue.HasValue ? redisValue : default;
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                var redisValue = Retry(() => Database.HashGet(hashKey, hashField));
                return redisValue.HasValue ? redisValue : default;
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        public object? HashGet(string hashKey, string hashField, System.Type dataType)
        {
            object? result = default;
            try
            {
                var redisValue = Database.HashGet(hashKey, hashField);
                return redisValue.HasValue ? GetValue(redisValue, dataType) : default;
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                var redisValue = Retry(() => Database.HashGet(hashKey, hashField));
                return redisValue.HasValue ? GetValue(redisValue, dataType) : default;
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        public string? HashGetOrDefault(string hashKey, string hashField)
        {
            var result = HashGet(hashKey, hashField);

            if (result is null && System.Enum.IsDefined(typeof(SettingKeys), hashKey))
            {
                var settingKey = (SettingKeys)System.Enum.Parse(typeof(SettingKeys), hashKey.ToString());
                result = settingKey.GetDefault<string>();
            }

            return result;
        }

        public object? HashGetOrDefault(string hashKey, string hashField, System.Type dataType)
        {
            object? result = default;
            try
            {
                var redisValue = Database.HashGet(hashKey, hashField);
                return redisValue.HasValue ? GetValue(redisValue, dataType) : default;
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                var redisValue = Retry(() => Database.HashGet(hashKey, hashField));
                return redisValue.HasValue ? GetValue(redisValue, dataType) : default;
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        /// <summary>
        /// Returns all fields and values of the hash stored at key.
        /// </summary>
        public HashEntry[]? HashGetAll(string hashKey)
        {
            HashEntry[]? entries = default;
            try
            {
                entries = Database.HashGetAll(hashKey);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                entries = Retry(() => Database.HashGetAll(hashKey));
                return entries;
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return entries;
        }

        public bool HashSet(string hashKey, HashEntry data, int cacheTime)
        {
            bool result = false;
            try
            {
                cacheTime += 1;
                ArgumentNullException.ThrowIfNull(data);
                result = Database.HashSet(hashKey, data.Name, data.Value);
                Database.KeyExpire(hashKey, TimeSpan.FromSeconds(cacheTime));
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                result = Retry(() => Database.HashSet(hashKey, data.Name, data.Value));
                Database.KeyExpire(hashKey, TimeSpan.FromSeconds(cacheTime));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            if (!result)
                _logger.Warning("{TITLE} not successful", ApiHelper.LogTitle());

            return result;
        }

        /// <summary>
        /// Sets the specified fields to their respective values in the hash stored at key.
        /// This command overwrites any specified fields that already exist in the hash, leaving other unspecified fields untouched.
        /// If key does not exist, a new key holding a hash is created.
        /// </summary>
        /// <param name="key">The key of the hash.</param>
        /// <param name="hashFields">The entries to set in the hash.</param>
        /// <param name="flags">The flags to use for this operation.</param>
        /// <remarks><seealso href="https://redis.io/commands/hmset"/></remarks>
        public bool HashSet(string hashKey, HashEntry[] data, int cacheTime)
        {
            bool result = false;
            try
            {
                cacheTime += 1;
                ArgumentNullException.ThrowIfNull(data);
                Database.HashSet(hashKey, data);
                var value = Database.HashGet(hashKey, data.FirstOrDefault().Name);
                result = value.HasValue;
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                Retry(() => Database.HashSet(hashKey, data));
                var value = Database.HashGet(hashKey, data.FirstOrDefault().Name);

                return value.HasValue;
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        public bool HashSet<T>(string hashKey, string hashField, T data, int cacheTime)
        {
            bool result = false;
            try
            {
                cacheTime += 1;
                ArgumentNullException.ThrowIfNull(data);
                var hashEntry = new HashEntry(hashField, JsonConvert.SerializeObject(data));
                result = Database.HashSet(hashKey, hashEntry.Name, hashEntry.Value);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                var hashEntry = new HashEntry(hashField, JsonConvert.SerializeObject(data));
                result = Retry(() => Database.HashSet(hashKey, hashEntry.Name, hashEntry.Value));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            if (!result)
                _logger.Warning("{TITLE} not successful", ApiHelper.LogTitle());

            return result;
        }

        /// <summary>
        /// Increment the specified field of an hash stored at key, and representing a floating point number, by the specified increment.
        /// If the field does not exist, it is set to 0 before performing the operation.
        /// </summary>
        public double HashIncrement(string hashKey, string hashField, double value)
        {
            double result = 0;
            try
            {
                result = Database.HashIncrement(hashKey, hashField, value);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                result = Retry(() => Database.HashIncrement(hashKey, hashField, value));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        /// <summary>
        /// Decrement the specified field of an hash stored at key, and representing a floating point number, by the specified decrement.
        /// If the field does not exist, it is set to 0 before performing the operation.
        /// </summary>
        /// 
        public double HashDecrement(string hashKey, string hashField, double value)
        {
            double result = 0;
            try
            {
                result = Database.HashDecrement(hashKey, hashField, value);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                result = Retry(() => Database.HashDecrement(hashKey, hashField, value));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        /// <summary>
        /// Returns if field is an existing field in the hash stored at key.
        /// </summary>
        /// <param name="key">The key of the hash.</param>
        /// <param name="hashField">The field in the hash to check.</param>
        /// <param name="flags">The flags to use for this operation.</param>
        /// <returns><see langword="true"/> if the hash contains field, <see langword="false"/> if the hash does not contain field, or key does not exist.</returns>
        /// <remarks><seealso href="https://redis.io/commands/hexists"/></remarks>
        public bool HashExists(string hashKey, string hashField)
        {
            bool result = false;
            try
            {
                result = Database.HashExists(hashKey, hashField);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                result = Retry(() => Database.HashExists(hashKey, hashField));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        public async Task<T?> HashGetOrUpdateAsync<T>(string hashKey, string hashField, T? data = default, int cacheTime = TTL.OneHour)
        {
            T? result = default;
            try
            {
                result = await ExecuteHashGetOrUpdateAsync(hashKey, hashField, data, cacheTime, result);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                result = await RetryAsync(() => ExecuteHashGetOrUpdateAsync(hashKey, hashField, data, cacheTime, result));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;

            async Task<T?> ExecuteHashGetOrUpdateAsync(string hashKey, string hashField, T? data, int cacheTime, T? result)
            {
                if (data != null) // update
                {
                    await HashSetAsync(hashKey, hashField, data, cacheTime);
                    result = await HashGetAsync<T>(hashKey, hashField);
                }
                else // get
                {
                    var isExists = await HashExistsAsync(CacheKeys.StaticData.ToString(), hashField);
                    if (isExists)
                        result = await HashGetAsync<T>(hashKey, hashField);
                    else
                        result = default;
                }

                return result;
            }
        }

        public T? HashGetOrUpdate<T>(string hashKey, string hashField, T? data = default, int cacheTime = TTL.OneHour)
        {
            T? result = default;
            try
            {
                result = ExecuteHashGetOrUpdate(hashKey, hashField, data, cacheTime, result);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    Reconnect();

                result = Retry(() => ExecuteHashGetOrUpdate(hashKey, hashField, data, cacheTime, result));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;

            T? ExecuteHashGetOrUpdate(string hashKey, string hashField, T? data, int cacheTime, T? result)
            {
                if (data != null) // update
                {
                    HashSet(hashKey, hashField.ToString(), data, cacheTime);
                    result = HashGet<T>(hashKey, hashField);
                }
                else // get
                {
                    var isExists = HashExists(CacheKeys.StaticData.ToString(), hashField.ToString());
                    if (isExists)
                        result = HashGet<T>(hashKey, hashField.ToString());
                    else
                        result = default;
                }

                return result;
            }
        }

        #endregion public Hash sync methods

        #region public Hash async methods

        /// <summary>
        /// Returns the value associated with field in the hash stored at key.
        /// </summary>
        public async Task<T?> HashGetAsync<T>(string hashKey, string hashField)
        {
            T? result = default;
            try
            {
                var redisValue = await Database.HashGetAsync(hashKey, hashField);
                result = GetValue<T>(hashField, redisValue);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                var redisValue = await RetryAsync(() => Database.HashGetAsync(hashKey, hashField));
                result = GetValue<T>(hashField, redisValue);
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        public async Task<T?> HashGetOrDefault<T>(string hashKey, string hashField)
        {
            T? result = default;
            try
            {
                var redisValue = await Database.HashGetAsync(hashKey, hashField);
                result = GetValueOrDefault<T>(hashField, redisValue);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                var redisValue = await RetryAsync(() => Database.HashGetAsync(hashKey, hashField));
                result = GetValueOrDefault<T>(hashField, redisValue);
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        public async Task<HashEntry[]?> HashGetAllAsync(string hashKey)
        {
            HashEntry[]? entries = default;
            try
            {
                entries = await Database.HashGetAllAsync(hashKey);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                entries = await RetryAsync(() => Database.HashGetAllAsync(hashKey));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return entries;
        }

        /// <summary>
        /// Sets the specified fields to their respective values in the hash stored at key.
        /// This command overwrites any specified fields that already exist in the hash, leaving other unspecified fields untouched.
        /// If key does not exist, a new key holding a hash is created.
        /// </summary>        
        public async Task<bool> HashSetAsync(string hashKey, string hashField, string data, int cacheTime = TTL.OneHour)
        {
            bool result = false;
            try
            {
                cacheTime += 1;
                ArgumentNullException.ThrowIfNull(data);
                result = await localHasAsync(hashKey, hashField, data, cacheTime, result);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                result = await RetryAsync(() => localHasAsync(hashKey, hashField, data, cacheTime, result));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            if (!result)
                _logger.Warning("{TITLE} not successful", ApiHelper.LogTitle());

            return result;

            async Task<bool> localHasAsync(string hashKey, string hashField, string data, int cacheTime, bool result)
            {
                if (await HashExistsAsync(hashKey, hashField))
                {
                    using var locker = await asyncLock.LockAsync(_lockTimeoutDefault);
                    var deleted = await Database.HashDeleteAsync(hashKey, hashField);
                    if (deleted)
                        result = await Database.HashSetAsync(hashKey, hashField, data);
                    await Database.KeyExpireAsync(hashKey, TimeSpan.FromSeconds(cacheTime));
                }
                else
                {
                    result = await Database.HashSetAsync(hashKey, hashField, data);
                    await Database.KeyExpireAsync(hashKey, TimeSpan.FromSeconds(cacheTime));
                }

                return result;
            }
        }

        public async Task<bool> HashSetAsync<T>(string hashKey, string hashField, T data, int cacheTime = TTL.OneHour)
        {
            bool result = false;
            try
            {
                cacheTime += 1;
                var value = data!.GetType() != typeof(string) ? JsonConvert.SerializeObject(data) : data?.ToString() ?? "";
                var hashValue = new RedisValue(value);

                if (await HashExistsAsync(hashKey, hashField))
                {
                    using var locker = await asyncLock.LockAsync(_lockTimeoutDefault);
                    var deleted = await Database.HashDeleteAsync(hashKey, hashField);
                    if (deleted)
                        result = await Database.HashSetAsync(hashKey, hashField, hashValue);
                    await Database.KeyExpireAsync(hashField, TimeSpan.FromSeconds(cacheTime));
                }
                else
                    result = await Database.HashSetAsync(hashKey, hashField, hashValue);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                result = await RetryAsync(async () =>
                {
                    var value = typeof(T) != typeof(string) ? JsonConvert.SerializeObject(data) : data?.ToString() ?? "";
                    var hashValue = new RedisValue(value);

                    if (await HashExistsAsync(hashKey, hashField))
                    {
                        var hashKeys = (await Database.HashGetAllAsync(hashKey)).ToList();
                        hashKeys.Add(new HashEntry(hashField, hashValue));
                        await Database.KeyExpireAsync(hashField, TimeSpan.FromSeconds(cacheTime));
                        return await HashSetAsync(hashKey, hashKeys.ToArray(), cacheTime);
                    }
                    else
                        return await Database.HashSetAsync(hashKey, hashField, hashValue);
                });
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            if (!result)
                _logger.Warning("{TITLE} not successful", ApiHelper.LogTitle());

            return result;
        }

        public async Task<bool> HashSetAsync(string hashKey, HashEntry[] data, int cacheTime = TTL.OneHour)
        {
            bool result = false;
            try
            {
                cacheTime += 1;
                ArgumentNullException.ThrowIfNull(data);
                if (await ExistsAsync(hashKey))
                {
                    var hashEntries = (await Database.HashGetAllAsync(hashKey)).ToList();
                    hashEntries.AddRange(data);

                    await Database.HashSetAsync(hashKey, hashEntries.ToArray());
                    await Database.KeyExpireAsync(hashKey, TimeSpan.FromSeconds(cacheTime));
                }
                else
                    await Database.HashSetAsync(hashKey, data);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                result = await RetryAsync(async () =>
                {
                    if (await ExistsAsync(hashKey))
                    {
                        var hashEntries = (await Database.HashGetAllAsync(hashKey)).ToList();
                        hashEntries.AddRange(data);

                        await Database.HashSetAsync(hashKey, hashEntries.ToArray());
                        await Database.KeyExpireAsync(hashKey, TimeSpan.FromSeconds(cacheTime));
                    }
                    else
                    {
                        await Database.HashSetAsync(hashKey, data);
                    }
                    return true;
                });
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            if (!result)
                _logger.Warning("{TITLE} not successful", ApiHelper.LogTitle());

            return result;
        }

        public async Task<bool> HashExistsAsync(string hashKey, string hashField)
        {
            bool result = false;
            try
            {
                result = await Database.HashExistsAsync(hashKey, hashField);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                result = await RetryAsync(() => Database.HashExistsAsync(hashKey, hashField));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        public async Task<bool> HashDeleteAsync(string hashKey, string hashField)
        {
            bool result = false;
            try
            {
                result = await Database.HashDeleteAsync(hashKey, hashField);
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                result = await RetryAsync(() => Database.HashDeleteAsync(hashKey, hashField));
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return result;
        }

        public async Task<IEnumerable<string>> HashKeysAsync(string hashKey)
        {
            IEnumerable<string>? results = [];
            try
            {
                RedisValue[] hashFields = await Database.HashKeysAsync(hashKey);
                results = hashFields.Select(x => x.ToString());
            }
            catch (Exception ex)
                when (ex.GetType() == typeof(RedisConnectionException) || ex.GetType() == typeof(RedisTimeoutException))
            {
                if (ex.GetType() == typeof(RedisConnectionException))
                    await ReconnectAsync();

                var hashFields = await RetryAsync(() => Database.HashKeysAsync(hashKey));
                results = hashFields?.Select(x => x.ToString());
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return results ?? [];
        }

        public bool HashExists(CacheKeys hashKey, CacheKeys hashField)
        {
            return HashExists(hashKey.ToString(), hashField.ToString());
        }

        #endregion public Hash async methods

        #region private methods

        private int ExecuteRemoveByPattern(string pattern)
        {
            int result = 0;
            foreach (EndPoint ep in Connection.GetEndPoints())
            {
                var server = Connection.GetServer(ep);
                var keys = server.Keys(pattern: $"*{pattern}*");

                foreach (RedisKey key in keys)
                {
                    var isDeleted = Database.KeyDelete(key);
                    if (isDeleted) result++;
                }
            }
            return result;
        }

        private async Task<int> ExecuteRemoveByPatternAsync(string pattern, int totalKeys)
        {
            foreach (EndPoint ep in Connection.GetEndPoints())
            {
                var server = Connection.GetServer(ep);
                var keys = server.KeysAsync(pattern: $"*{pattern}*");

                await foreach (RedisKey key in keys)
                {
                    var isDeleted = await Database.KeyDeleteAsync(key);
                    if (isDeleted) totalKeys++;
                }
            }
            return totalKeys;
        }

        private bool IsHashValueExist(string hashKey, string hashValue)
        {
            bool valueExists = false;
            HashEntry[] allFields = Database.HashGetAll(hashKey);

            // Iterate through all fields to check if any value matches the one you're looking for
            foreach (HashEntry entry in allFields)
            {
                if (entry.Value == hashValue)
                {
                    valueExists = true;
                    break;
                }
            }
            return valueExists;
        }

        private TValue? GetValue<TValue>(string hashKey, RedisValue redisValue, System.Type? resourceType = default)
        {
            TValue? result = default;
            try
            {
                if (typeof(TValue) == typeof(object) && System.Enum.IsDefined(typeof(SettingKeys), hashKey))
                {
                    var settingKey = (SettingKeys)System.Enum.Parse(typeof(SettingKeys), hashKey);
                    var resourseType = resourceType ?? settingKey.GetResourceType();

                    string jsonString = redisValue.ToString();
                    if (hashKey == SettingKeys.UmbracoSettings.ToString())
                        jsonString = ApiConvertHelper.ToUmbracoList(redisValue.ToString());

                    var objectResult = JsonConvert.DeserializeObject(jsonString, resourseType);
                    result = (TValue?)objectResult;
                }
                else if (typeof(TValue) == typeof(string))
                    result = (TValue)Convert.ChangeType(redisValue.ToString(), typeof(TValue));
                else
                    result = JsonConvert.DeserializeObject<TValue?>(redisValue.ToString());
            }
            catch (Exception ex)
            {
                Log.Logger.Warning("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            return result;
        }

        private TValue? GetValueOrDefault<TValue>(string hashKey, RedisValue redisValue)
        {
            TValue? result = GetValue<TValue>(hashKey, redisValue);

            if (result is null && System.Enum.IsDefined(typeof(SettingKeys), hashKey))
            {
                var settingKey = (SettingKeys)System.Enum.Parse(typeof(SettingKeys), hashKey.ToString());
                result = settingKey.GetDefault<TValue>();
            }

            return result;
        }

        public static object? GetValue(RedisValue redisValue, System.Type resourceType)
        {
            object? objectResult = default;
            try
            {
                var jsonString = redisValue.ToString() ?? "";
                var byteArray = Encoding.UTF8.GetBytes(jsonString);
                var stream = new MemoryStream(byteArray);

                if (resourceType == typeof(string))
                    objectResult = jsonString.ToString();
                else
                    objectResult = JsonConvert.DeserializeObject(jsonString, resourceType);
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} exception on the resourceType deserialize: {RESOURCETYPE}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), resourceType.FullName, ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return objectResult;
        }

        public static object? GetValueOrDefault(RedisValue redisValue, System.Type resourceType)
        {
            object? objectResult = default;
            try
            {
                var jsonString = redisValue.ToString() ?? "";
                objectResult = JsonConvert.DeserializeObject<object>(jsonString);
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} exception on the resourceType deserialize: {RESOURCETYPE}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), resourceType.FullName, ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return objectResult;
        }

        public async Task<T?> RetryAsync<T>(Func<Task<T>> executeAsync)
        {
            int retryCount = 0;
            T? result = await Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(_appSettings.Redis.RetryAttempts,
                    retryAttempt => TimeSpan.FromSeconds(_appSettings.Redis.RetryInterval + (retryAttempt - 1) * 2),
                    (exception, timeSpan) =>
                    {
                        retryCount++;
                        Log.Warning("ErrorInfo to execute the redis operation. Retrying in {Delay} sec.", _appSettings.Redis.RetryInterval);
                    })
                .ExecuteAsync(executeAsync)
                .ContinueWith(result =>
                {
                    if (result.IsFaulted)
                    {
                        Log.Error(result.Exception, $"{ApiHelper.LogTitle()} {result.Exception?.Message ?? "failed to execute operation"}");
                        return default;
                    }
                    return result.Result;
                });
            return result;
        }

        public async Task RetryAsync(Func<Task> executeAsync)
        {
            await RetryAsync<int>(async () =>
            {
                await executeAsync(); return -1;
            });
        }

        public T? Retry<T>(Func<T> execute)
        {
            int retryCount = 0;
            T? result = Policy
                .Handle<Exception>()
                .WaitAndRetry(_appSettings.Redis.RetryAttempts,
                    retryAttempt => TimeSpan.FromSeconds(_appSettings.Redis.RetryInterval + (retryAttempt - 1) * 2),
                    (exception, timeSpan) =>
                    {
                        retryCount++;
                        Log.Warning("ErrorInfo to execute the redis operation. Retrying({RETRYCOUNT}) in {DELAY} sec.", retryCount, _appSettings.Redis.RetryInterval);
                    }
                )
                .Execute(execute);
            return result;
        }

        public void Retry(Action execute)
        {
            Retry<int>(() =>
            {
                execute(); return -1;
            });
        }

        #endregion private methods
    }
}
