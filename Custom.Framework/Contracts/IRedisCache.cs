using Custom.Framework.Cache;
using Custom.Framework.Services;
using StackExchange.Redis;

namespace Custom.Framework.Contracts
{
    public interface IRedisCache : IApiWorkerBase
    {
        string RedisName { get; set; }
        IConnectionMultiplexer Connection { get; set; }

        #region Get
        string? Get(string key);
        object? Get(string key, Type? resourseType = null);

        T? Get<T>(string key);
        Task<T?> GetAsync<T>(string key);
        Task<object?> GetAsync(string key, Type? resourceType = null);
        Dictionary<string, T?> GetAll<T>(string pattern);
        Task<Dictionary<string, T?>> GetAllAsync<T>(string pattern);

        string? HashGet(string hashKey, string hashField);
        string? HashGetOrDefault(string hashKey, string hashField);

        object? HashGet(string hashKey, string hashField, Type dataType);
        object? HashGetOrDefault(string hashKey, string hashField, Type dataType);

        T? HashGet<T>(string hashKey, string hashField);

        Task<T?> HashGetAsync<T>(string hashKey, string hashField);
        Task<T?> HashGetOrDefault<T>(string hashKey, string hashField);

        T? HashGetOrUpdate<T>(string hashKey, string hashField, T? data = default, int cacheTime = TTL.OneHour);
        HashEntry[]? HashGetAll(string hashKey);
        Task<HashEntry[]?> HashGetAllAsync(string hashKey);

        T? GetOrCreate<T>(string key, Func<T> actionToGetData, int ttl, bool isUseRedis);

        #endregion Get

        #region Set
        bool Set<T>(string key, T value, int cacheTime = TTL.OneHour);
        Task<bool> SetAsync<T>(string key, T value, int cacheTime = TTL.OneHour);

        bool HashSet(string hashKey, string hashField, string value, int cacheTime = TTL.OneHour);
        bool HashSet(string hashKey, HashEntry data, int cacheTime = TTL.OneHour);
        bool HashSet(string hashKey, HashEntry[] data, int cacheTime = TTL.OneHour);
        bool HashSet<T>(string hashKey, string hashField, T data, int cacheTime = TTL.OneHour);

        Task<bool> HashSetAsync(string hashKey, string hashField, string data, int cacheTime = TTL.OneHour);
        Task<bool> HashSetAsync(string hashKey, HashEntry[] data, int cacheTime = TTL.OneHour);
        Task<bool> HashSetAsync<T>(string hashKey, string hashField, T data, int cacheTime = TTL.OneHour);
        Task<T?> HashGetOrUpdateAsync<T>(string hashKey, string hashField, T? data = default, int cacheTime = TTL.OneHour);
        Task<IEnumerable<string>> HashKeysAsync(string hashKey);

        double HashIncrement(string hashKey, string hashField, double value);
        double HashDecrement(string hashKey, string hashField, double value);
        Task<bool> HashDeleteAsync(string hashKey, string hashField);

        long StringIncrement(string key);

        #endregion Set with CacheKey

        #region Exist

        bool HashExists(string hashkey, string hashField);
        Task<bool> HashExistsAsync(string hashkey, string hashField);
        bool Exists(string key);
        Task<bool> ExistsAsync(string key);
        Task<bool> ExistsAsync(string[] keys);

        #endregion Exist

        #region Remove
        bool Remove(string key);
        Task<bool> RemoveAsync(string key);
        int RemoveByPattern(string pattern);
        Task<int> RemoveByPatternAsync(string pattern);

        #endregion Remove

        #region Connect
        bool IsConnected();
        bool Reconnect(TimeSpan? timeSpan = null);
        Task<bool> ReconnectAsync(TimeSpan? timeSpan = null);
        string ReconnectTimeStamp { get; }
        #endregion Connect
    }
}