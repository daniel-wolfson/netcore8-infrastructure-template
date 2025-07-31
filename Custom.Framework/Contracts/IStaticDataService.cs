using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Models.Base;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Custom.Framework.StaticData.Contracts
{
    public interface IStaticDataService
    {
        /// <summary>
        /// DataSource - Static data source based on memoryCache
        /// </summary>
        StaticDataCollection<EntityData> DataContext { get; }

        IServiceScope StaticDataServiceScope { get; }

        /// <summary>
        /// GetFromStaticList - Get data from Nop
        /// </summary>
        IServiceResult<List<EntityData>> GetFromStaticList(IEnumerable<string>? settingKeys = null);

        /// <summary>
        /// GetFromNop - Get data from Nop
        /// </summary>
        Task<IServiceResult<List<EntityData>>> GetFromNop();

        /// <summary>
        /// GetFromDb - Get data from Nop
        /// </summary>
        Task<IServiceResult<List<EntityData>>> GetCurrencyRates();

        /// <summary>
        /// GetFromUmbraco - Get dat providers
        /// </summary>
        Task<IServiceResult<List<EntityData>>> GetFromUmbraco(IEnumerable<string>? settingKeys = null);

        /// <summary>
        /// GetFromOptima - Get data from Optima
        /// </summary>
        Task<IServiceResult<List<EntityData>>> GetFromOptima(IEnumerable<string>? settingKeys = null);

        /// <summary>
        /// GetFromDb - Get data from Db
        /// </summary>
        Task<IServiceResult<List<EntityData>>> GetCurrencyRatesAsync(IEnumerable<string>? settingKeys = null);

        /// <summary>
        /// GetFromBlobAsync - Get data from Blob
        /// </summary>
        Task<IServiceResult<List<EntityData>>> GetFromBlobAsync(string version, IEnumerable<string>? settingKeys = null);

        /// <summary>
        /// GetFromRedis - Get data from redis
        /// </summary>
        Task<IServiceResult<List<EntityData>>> GetFromRedis(string version, IEnumerable<string>? settingKeys = null);

        /// <summary>
        /// UpdateToBlob - Update data to Blob
        /// </summary>
        Task<IServiceResult<List<Result>>> UpdateToBlob(string version, IEnumerable<string>? settingKeys = null);

        /// <summary>
        /// UpdateFromBlobsToRedis - Update data to Redis
        /// </summary>
        Task<IServiceResult<List<Result>>> UpdateToRedis(string version, IEnumerable<string>? settingKeys = null);

        /// <summary>
        /// LoadStaticData - Get all static data
        /// </summary>
        Task LoadStaticData(string providerType = ProviderTypes.All, IEnumerable<string>? settingKeys = null);

        /// <summary>
        /// PreloadStaticData - Get all static data
        /// </summary>
        Task PreloadStaticData(string providerType = ProviderTypes.All, IEnumerable<string>? settingKeys = null);

        /// <summary>
        /// GetOrCreateData - Get or create data from settingKey using memory cache
        /// </summary>
        Task<object> GetOrCreateData(SettingKeys settingKey, Func<ICacheEntry, Task<object>>? factory = null);

        void SetToMemoryCache(IEnumerable<string>? settingKeys = null);

        bool UseMemoryCache { get; set; }

        /// <summary> 
        /// Exist staticData in memory cache
        /// </summary>
        //bool ExistStaticData { get; }
        List<string> UndefinedStaticData { get; }

        List<string> InMemoryStaticData { get; }

        List<ConfigData> DataSections { get; set; }
    }
}