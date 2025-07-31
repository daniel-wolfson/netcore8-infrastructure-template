using Custom.Framework.Cache;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.ComponentModel;

namespace Custom.Framework.Configuration
{
    public class ApiConfigurationSource : MemoryConfigurationSource, IConfigurationSource, IApiConfigurationSource
    {
        //private readonly Lazy<object> _defaultValue;
        private IServiceScopeFactory _serviceScopeFactory;
        private IServiceProvider _serviceProvider;
        private IConfiguration _configuration;
        private ILogger _logger;
        private object? _currentValue = null;

        public ApiConfigurationSource(
            IServiceScopeFactory serviceScopeFactory,
            IServiceProvider serviceProvider,
            ILogger logger,
            IConfiguration configuration)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            //_defaultValue = new Lazy<object>(GetLazyDefaultValue);
        }

        #region props

        //public object DefaultValue => _defaultValue.Code;

        [DefaultValue(SettingKeys.StaticData)]
        public SettingKeys RootSettingKey { get; set; }

        [DefaultValue(SettingKeys.Unknown)]
        public SettingKeys SettingKey { get; set; }

        public int Order { get; set; }

        [DefaultValue(typeof(object))]
        public string ResourceType { get; set; }

        [DefaultValue(TTL.Timeout)]
        public int? ReloadTimeout { get; set; }

        public bool? ReloadOnChange { get; set; }

        [DefaultValue(TTL.None)]
        public int? ReloadInterval { get; set; }

        [DefaultValue(ProviderTypes.AzureStorage)]
        public string SourceType { get; set; }

        [DefaultValue(ProviderTypes.Optima)]
        public string OriginalSourceType { get; set; }

        [DefaultValue(TTL.Default)]
        public string Ttl { get; set; }

        public bool EnableLoadBySourceType { get; set; }

        public object? CurrentValue => _currentValue; // ??= DefaultValue

        #endregion props

        #region public methods

        public IConfiguration GetConfiguration()
        {
            return _configuration;
        }

        public IServiceScopeFactory ServiceScopeFactory => _serviceScopeFactory;
        public IServiceProvider ServiceProvider => _serviceProvider;

        public new IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return ApiConfigurationFactory.MakeSourceProvider(SettingKey, this);
        }

        /// <summary>
        /// Set current value object that it will used as configuration.GetValueOrDefault<...>(SettingsKey)
        /// </summary>
        public void SetCurrentValue(object value)
        {
            _currentValue = value;
        }

        #endregion public methods

        #region private methods

        private async Task<object> GetLazyDefaultValue()
        {
            object? objectData = default;
            try
            {
                var serviceScope = _serviceScopeFactory.CreateScope();
                var resourceType = SettingKey.GetResourceType();
                var jsonDefault = SettingKey.GetJsonDefault();

                var appSettings = serviceScope.ServiceProvider.GetService<IOptions<ApiSettings>>()?.Value!;
                var cashKey = $"{SettingKeys.StaticData}_{appSettings.Version}";

                var redisCache = serviceScope.ServiceProvider.GetService<IRedisCache>();
                objectData = redisCache?.HashGet(cashKey, SettingKey.ToString(), resourceType);

                if (objectData == null || (objectData != null && objectData.ToString() == jsonDefault))
                {
                    var provider = (ApiConfigurationProvider)serviceScope.ServiceProvider.GetKeyedService<IConfigurationProvider>(SettingKey)!;
                    objectData = await provider.GetAsync<object>(SettingKey);
                    objectData = redisCache?.HashGetOrUpdate(cashKey, SettingKey.ToString(), objectData);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            objectData ??= SettingKey.GetDefault();
            return objectData;
        }

        #endregion private methods
    }
}