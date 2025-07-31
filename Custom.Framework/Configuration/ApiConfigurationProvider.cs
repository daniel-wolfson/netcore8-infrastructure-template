using FluentValidation;
using Custom.Framework.Cache;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Configuration.Optima;
using Custom.Framework.Configuration.Umbraco;
using Custom.Framework.Contracts;
using Custom.Framework.Exceptions;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Custom.Framework.Configuration
{
    public class ApiConfigurationProvider : MemoryConfigurationProvider, IConfigurationProvider, IDisposable
    {
        private static readonly object _lock = new();

        #region fields

        private ApiSettings? _appSettings = default;
        private ILogger? _logger;
        private IConfiguration? _configuration;
        private IRedisCache? _redisCache;
        private IBlobStorage? _blobStorage;
        private IServiceScope? _localServiceScope;

        public ApiConfigurationProvider(IServiceScopeFactory serviceScopeFactory, ApiConfigurationSource source) : base(source)
        {
            Source = source;
            ServiceScopeFactory = serviceScopeFactory;
        }

        #endregion fields

        #region props

        public ApiConfigurationSource Source { get; set; }

        public string RootCacheKey => $"{CacheKeys.StaticData}_{AppSettings.Version}";

        public SettingKeys RootSettingKey { get; set; } = SettingKeys.StaticData;

        public SettingKeys? SettingKey => Source.SettingKey;

        public IServiceScopeFactory ServiceScopeFactory { get; set; }

        protected IServiceScope LocalServiceScope
        {
            get
            {
                _localServiceScope ??= ServiceScopeFactory.CreateScope();
                return _localServiceScope;
            }
            set { _localServiceScope = value; }
        }

        protected ILogger Logger => _logger ??= GetService<ILogger>();

        protected IConfiguration Configuration => _configuration ??= GetService<IConfiguration>();

        protected ApiSettings AppSettings => _appSettings ??= GetService<IOptions<ApiSettings>>().Value;

        protected IRedisCache RedisCache => _redisCache ??= GetService<IRedisCache>();

        protected IBlobStorage BlobStorage => _blobStorage ??= GetService<IBlobStorage>();

        #endregion props

        #region public override methods

        /// <summary>
        /// SettingsLoad-step-5 (ApiConfigurationProvider => Load)
        /// Load, it occurs by add to sources, Reload-step
        /// </summary>
        public override void Load()
        {
            /// SettingsLoad-step-6 (ApiConfigurationProvider => call LoadAsync)
            LoadAsync(SettingKey ?? RootSettingKey, EReasonTypes.StartupLoad).GetAwaiter().GetResult();
        }

        public override bool TryGet(string key, out string? value)
        {
            return base.TryGet(key, out value);
        }

        public override void Set(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value))
                base.Set(key, value);
        }

        /// <summary>
        /// Set configuration setting's keys from cache or blob
        /// </summary>
        public void Set(SettingKeys settingKey, string value)
        {
            Set($"{SettingKeys.StaticData}:{settingKey}", value);
        }

        #endregion override methods

        #region public and protected methods

        /// <summary>
        /// Set (after get async) the configuration setting's keys
        /// </summary>
        public virtual Task<(bool IsSuccess, EReasonTypes reason, string Message)> SetAsync(SettingKeys settingKey, object objectResult)
        {
            string? setJson = default;
            string message = string.Empty;
            bool isSuccess = false;
            EReasonTypes reason = EReasonTypes.NotUpdated;

            try
            {
                if (objectResult.GetType() == typeof(string))
                    setJson = objectResult.ToString();
                else
                    setJson = Newtonsoft.Json.JsonConvert.SerializeObject(objectResult);

                var key = $"{RootSettingKey}:{settingKey}";
                var lastJsonResult = Data.ContainsKey(key) ? Data[key] : "";

                var jsonValidationResult = ApiJsonHelper.Validate(settingKey, lastJsonResult, setJson);
                if (jsonValidationResult.IsValid)
                {
                    lock (_lock)
                    {
                        Set(key, setJson);
                        Set($"{key}:Timestamp", DateTime.Now.ToString("yyyyMMddHHmmssff"));

                        Source.SetCurrentValue(objectResult);
                        isSuccess = true;
                        OnReload();
                    }

                    reason = EReasonTypes.Updated;
                    message = $"{SettingKey} updated and configured successfully";
                }
                else
                {
                    message = $"{SettingKey} {jsonValidationResult.Message}";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("{TITLE} {SETTINGKEY} loading failed and will to use last saved value. ErrorInfo: {ERROR}",
                    ApiHelper.LogTitle(), SettingKey, ex.InnerException?.Message ?? ex.Message);
                reason = EReasonTypes.Fail;
            }

            return Task.FromResult<(bool IsSuccess, EReasonTypes reason, string Message)>((isSuccess, reason, message));
        }

        /// <summary> GetAsync2: get data by two steps: 
        /// <br>1.  RedisCache.GetAsync2</br>
        /// <br>2.  BlobStorage.DownloadAsync, (it will occur if (1) is empty </br>
        /// </summary>
        public virtual Task<IServiceResult<TData>> GetAsync<TData>(
            SettingKeys settingKey, CancellationToken cancellationToken = default)
            where TData : class
        {
            string message = string.Empty;
            TData? data = default;

            try
            {
                data = settingKey.GetDefault<TData>();
                return Task.FromResult(ServiceResult<TData>.Default(data));
            }
            catch (Exception ex)
            {
                _logger?.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                data = settingKey.GetDefault<TData>();
                return Task.FromResult(ServiceResult<TData>.Default(data));
            }
        }

        /// <summary> 
        /// SettingsLoad-step-7 (ApiConfigurationProvider => Loading data from BlobStorage.DownloadAsync/RedisCache.HashGetAsync)
        /// </summary>
        public virtual async Task<IServiceResult<object>> LoadAsync(SettingKeys settingKey, EReasonTypes reason, CancellationToken cancelToken = default)
        {
            try
            {
                Logger.Debug("{TITLE} {SETTINGKEY} reloading by reason - {REASON}", ApiHelper.LogTitle(), settingKey, reason);
                var configKey = $"{SettingKeys.StaticData}:{SettingKey}";

                /// SettingsReload-step-1 (Init - set on first time the key-value data into configuration, if it not exist)
                if (!Data.ContainsKey(configKey))
                    Set(settingKey, settingKey.GetJsonDefault());

                if (this.Source.EnableLoadBySourceType)
                {
                    /// SettingsReload-step-2 (GetAsync2 - get from data provider by settingKey source type)
                    var objectResult = await GetAsync<object>(settingKey, cancelToken);

                    /// SettingsReload-step-3 (SetAsync - set value into configuration)
                    if (objectResult.IsSuccess)
                    {
                        var setResult = await SetAsync(settingKey, objectResult.Value ?? settingKey.GetResourceType().GetDefault());

                        if (setResult.IsSuccess)
                            Logger.Debug("{TITLE} {SETTINGKEY} updated and configured successfully", ApiHelper.LogTitle(), settingKey);
                        else
                        {
                            if (setResult.reason == EReasonTypes.NotUpdated)
                                Logger.Debug("{TITLE} {MESSAGE}", ApiHelper.LogTitle(), setResult.Message);
                            else
                                Logger.Warning("{TITLE} {MESSAGE}", ApiHelper.LogTitle(), setResult.Message);
                        }
                        //}
                    }
                    else
                    {
                        Logger.Error("{TITLE} {SETTINGKEY} loading failed and will to use last saved value. ErrorInfo: {ERROR}",
                            ApiHelper.LogTitle(), settingKey, objectResult.Message);
                    }
                    return objectResult;
                }
                else
                {
                    var data = settingKey.GetDefault();
                    return ServiceResult<object>.Default(data);
                }
            }
            catch (Exception ex)
            {
                var errMsg = ex.InnerException?.Message ?? ex.Message;
                Logger.Error("{TITLE} {SETTINGKEY} loading failed and will to use last saved value. ErrorInfo: {ERROR}",
                    ApiHelper.LogTitle(), settingKey, ex.InnerException?.Message ?? ex.Message);
                Set(settingKey, settingKey.GetJsonDefault());

                return ServiceResult<object>.Error(errMsg);
            }
        }

        /// <summary> Validate jsonResult used the special validator </summary>
        protected async Task<string> ValidateAsync<TRequest>(IValidator<TRequest> validator, TRequest data)
        {
            if (data == null)
            {
                var errorMessage = new ApiException(ServiceStatus.Conflict, $"{nameof(data)}").ToString();
                return errorMessage;
            }

            var validationResult = await validator.ValidateAsync(data);
            if (!validationResult.IsValid)
            {
                var errorMessage = string.Join("\n\t", validationResult.Errors.Select(err => $"{err.ErrorMessage}"));
                return errorMessage;
            }

            return "";
        }

        public virtual Task ExecuteAllAsync(List<SettingKeys> settingKeysList)
            => Task.CompletedTask;

        /// <summary> GetService go to get the instance of TData from registered services </summary>
        protected T GetService<T>(string? serviceKey = null)
        {
            if (!string.IsNullOrEmpty(serviceKey))
                return LocalServiceScope.ServiceProvider.GetKeyedService<T>(serviceKey)!;

            var service = LocalServiceScope.ServiceProvider.GetService<T>()
                ?? throw new ApiException(ServiceStatus.FatalError, $"{ApiHelper.LogTitle()} error: {typeof(T).Name} not registered");

            return service;
        }

        protected virtual T? ParseContent<T>(SettingKeys settingsKey, string content)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<T?>(content);
                return data;
            }
            catch (Exception ex)
            {
                Logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                var data = typeof(T?).GetDefault<T?>();
                return data;
            }
        }

        public void Dispose()
        {
            //LocalServiceScope?.Dispose();
        }

        #endregion public and protected methods
    }
}

