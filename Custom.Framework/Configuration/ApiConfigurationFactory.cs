using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Exceptions;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Custom.Framework.Configuration
{
    /// <summary>
    /// StaticDataConfigurationFactory - InitConfigurationSources, MakeSourceProvider, RunTimerAsync
    /// </summary>
    public class ApiConfigurationFactory : IApiConfigurationFactory
    {
        #region fields

        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _serviceFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        #endregion fields

        #region ctor

        public ApiConfigurationFactory(ILogger logger,
            IServiceScopeFactory factory,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _serviceFactory = factory;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        #endregion ctor

        #region public methods

        /// <summary>
        /// SettingsLoad-step-2 (ApiConfigurationFactory => InitConfigurationSources)
        /// it reads the configuration from appSettings 
        /// and add this source to load queue (SettingsTaskReloadQueue)
        /// </summary>
        public Task InitConfigurationSources(SettingKeys rootSectionSettingKey, CancellationToken? cancelToken = null)
        {
            try
            {
                cancelToken ??= CancellationToken.None;
                var appSettingSections = _configuration.GetSections(SettingKeys.StaticData).ToDataList();

                if (appSettingSections.Count != 0)
                {
                    _logger.Debug("{TITLE} Config initializing... ", ApiHelper.LogTitle());

                    // SettingsLoad-step-3 (AddConfigurationSource for all settingKeys)
                    appSettingSections.ForEach(section => AddConfigurationSource(section.SettingKey));

                    // SettingsLoad-step-4 (AddConfigurationSource)
                    var notInitializedList = appSettingSections
                        .Where(section => _configuration[$"{SettingKeys.StaticData}:{section.SettingKey}"] == null)
                        .ToList();

                    if (notInitializedList.Count == 0)
                        _logger.Debug("{TITLE} Config initialized successfully", ApiHelper.LogTitle());
                    else
                        _logger.Error("{TITLE} SettingsKeys: {KEYS} not initialized", 
                            ApiHelper.LogTitle(), string.Join(", ", notInitializedList.Select(x => x.SettingKey)));

                    _logger.Debug("");
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} setting exception: {EXCEPTION}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                throw new ApiException(ServiceStatus.FatalError, ex);
            }
        }

        /// <summary> 
        /// Makes the configuration source provider from appSettings by sourceType 
        /// </summary>
        public static IConfigurationProvider MakeSourceProvider(SettingKeys settingsKey, ApiConfigurationSource source)
        {
            try
            {
                var provider = source.ServiceProvider.GetKeyedService<IConfigurationProvider>(settingsKey)!;
                return provider;
            }
            catch (Exception ex)
            {
                throw new ApiException(ServiceStatus.FatalError,
                    $"{ApiHelper.LogTitle()} exception: {ex.InnerException?.Message ?? ex.Message}", ex);
            }
        }

        /// <summary> 
        /// Run processing for Reload ItemsSettings contained into the SettingsTaskReloadQueue 
        /// </summary>
        public virtual async Task RunTimerAsync(SettingKeys settingKey, CancellationToken cancelationToken)
        {
            try
            {
                var isReloadIntervalDefined = int.TryParse(_configuration[$"{SettingKeys.StaticData}:{settingKey}:ReloadInterval"], out int reloadInterval);

                _logger.Information("{TITLE} Reload started for key {SETTINGKEY}. Interval: {RELOADINTERVAL}sec.",
                    ApiHelper.LogTitle(), settingKey, reloadInterval);

                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(reloadInterval));

                while (!cancelationToken.IsCancellationRequested
                    && await timer.WaitForNextTickAsync(cancelationToken))
                {
                    //if (!SettingsTaskReloadQueue.Contains(settingKey))
                    //    SettingsTaskReloadQueue.EnqueueTask(settingKey, MakeReloadTask(settingKey));
                }
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                   ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
        }

        #endregion public methods

        #region privates methods

        /// <summary> 
        /// AddConfigurationSource
        /// </summary>
        private void AddConfigurationSource(SettingKeys settingKey)
        {
            try
            {
                using var scope = _serviceFactory.CreateScope();
                var source = scope.ServiceProvider.GetKeyedService<IConfigurationSource>(settingKey)!;
                var configurationManager = scope.ServiceProvider.GetService<IConfigurationManager>();

                if (configurationManager != null &&
                    !configurationManager.Sources.OfType<ApiConfigurationSource>()
                        .Any(x => x.SettingKey == settingKey))
                {
                    /// SettingsLoad-step-4 (AddConfigurationSource => Sources.Add => call Load)
                    // Warning, Sources.Add make load event from source provider, if it was defined,
                    // provider (IConfigurationProvider) defined in program file as singelton
                    // see comment "...-step-..."
                    configurationManager?.Sources.Add(source);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                   ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
        }

        #endregion privates methods
    }
}