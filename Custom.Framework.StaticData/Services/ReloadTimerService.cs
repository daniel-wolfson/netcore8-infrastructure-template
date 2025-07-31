using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Helpers;
using Custom.Framework.StaticData.Confiuration;
using Custom.Framework.StaticData.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

namespace Custom.Framework.StaticData.Services
{
    public class ReloadTimerService : BackgroundService
    {
        private readonly TimeSpan _period = TimeSpan.FromSeconds(20);
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceScopeFactory _serviceFactory;
        private readonly ILogger _logger;
        private readonly IApiReloadTaskQueue _taskQueue;
        private readonly IOptionsMonitor<RequestMiddlewareOptions> _requestOptionsMonitor;

        public ReloadTimerService(ILogger logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            IServiceScopeFactory serviceFactory,
            IApiReloadTaskQueue taskQueue,
            IOptionsMonitor<RequestMiddlewareOptions> extensionsOptionsMonitor)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _serviceFactory = serviceFactory;
            _taskQueue = taskQueue;
            _requestOptionsMonitor = extensionsOptionsMonitor;
        }

        public RequestMiddlewareOptions RequestMiddlewareOptions => _requestOptionsMonitor.CurrentValue;

        public EBackgroundServiceStatus State { get; set; }

        #region public methods

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"{ApiHelper.LogTitle()} Started...");

            await using AsyncServiceScope asyncScope = _serviceFactory.CreateAsyncScope();
            var factory = asyncScope.ServiceProvider.GetRequiredService<IApiConfigurationFactory>();
            cancellationToken = _requestOptionsMonitor.CurrentValue.StaticDataCancellationSource.Token;

            var settingKeys = _configuration.GetSections(SettingKeys.StaticData)
                .Where(x => x.ReloadInterval > 0 && x.Provider == "OptimaMainApi")
                .Select(x => x.SettingKey)
                .ToList();

            //await Task.WhenAll(settingKeys.Select(settingKey =>
            //factory.RunTimerAsync(settingKey, cancellationToken)));
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _requestOptionsMonitor.CurrentValue.StaticDataCancellationSource = new CancellationTokenSource();
                _requestOptionsMonitor.CurrentValue.StaticDataCancellationToken = _requestOptionsMonitor.CurrentValue.StaticDataCancellationSource.Token;
                cancellationToken = _requestOptionsMonitor.CurrentValue.StaticDataCancellationToken;

                return base.StartAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                var errMsg = ex.InnerException?.Message ?? ex.Message;
                Log.Logger.Error("{TITLE} failed: {MESSAGE}", ApiHelper.LogTitle(), errMsg);
                return Task.FromException(ex);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken = _requestOptionsMonitor.CurrentValue.StaticDataCancellationToken;
                _requestOptionsMonitor.CurrentValue.StaticDataCancellationSource.Cancel();

                await base.StopAsync(cancellationToken);

                _logger.Information("{TITLE} stopped.", ApiHelper.LogTitle());
            }
            catch (Exception ex)
            {
                var errMsg = ex.InnerException?.Message ?? ex.Message;
                Log.Logger.Error("{TITLE} failed: {MESSAGE}", ApiHelper.LogTitle(), errMsg);
            }
        }

        #endregion public methods

        #region private methods

        private void ShowConfigurations()
        {
            _logger.Debug(_configuration[SettingKeys.StaticData.ToString()]!);
        }

        private void ReloadConfigurations()
        {
            //ExtensionsConfigurationDataSource.Register("eventHubs", new EventHubsOptions());
            // The following code force reload the IConfiguration object.
            if (_configuration != null)
            {
                IConfigurationRoot? root = _configuration as IConfigurationRoot;
                root?.Reload();
            }
        }

        #endregion private methods

    }
}
