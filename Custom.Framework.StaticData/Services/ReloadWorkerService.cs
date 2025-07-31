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
    public class ReloadWorkerService(ILogger logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IServiceScopeFactory serviceFactory,
        IApiReloadTaskQueue taskQueue,
        IOptionsMonitor<RequestMiddlewareOptions> extensionsOptionsMonitor) : BackgroundService
    {
        #region fields and props

        private static bool IsActive = true;
        private readonly TimeSpan _period = TimeSpan.FromSeconds(20);
        private readonly IConfiguration _configuration = configuration;
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly IServiceScopeFactory _serviceFactory = serviceFactory;
        private readonly ILogger _logger = logger;
        private readonly IApiReloadTaskQueue _taskQueue = taskQueue;
        private readonly IOptionsMonitor<RequestMiddlewareOptions> _requestOptionsMonitor = extensionsOptionsMonitor;

        public RequestMiddlewareOptions RequestMiddlewareOptions => _requestOptionsMonitor.CurrentValue;
        public EBackgroundServiceStatus State { get; set; }

        #endregion fields and props

        #region public methods

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"{ApiHelper.LogTitle()} Started...");

            await using AsyncServiceScope asyncScope = _serviceFactory.CreateAsyncScope();
            var factory = asyncScope.ServiceProvider.GetRequiredService<IApiConfigurationFactory>();
            cancellationToken = _requestOptionsMonitor.CurrentValue.StaticDataCancellationSource.Token;

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(5000, cancellationToken);
                //if (!IsActive || factory.SettingsTaskReloadQueue.IsEmpty) continue;

                //var reloadQueueItem = await factory.SettingsTaskReloadQueue.DequeueAsync(cancellationToken);
                //if (reloadQueueItem == null) continue;

                //var settingKey = $"{SettingKeys.StaticList}:{reloadQueueItem.SettingKey}";
                //var isReloadIntervalDefined = int.TryParse(_configuration[$"{settingKey}:ReloadInterval"], out int reloadInterval);
                //if (!isReloadIntervalDefined) continue;

                //var isReloadTimeoutDefined = int.TryParse(_configuration[$"{settingKey}:ReloadTimeout"], out int reloadTimeout);
                //if (!isReloadIntervalDefined) reloadTimeout = reloadTimeoutDefault;

                //var reloadTask = reloadQueueItem.GetReloadTask(_serviceFactory, cancellationToken);
                //var timeoutTask = Task.Delay(TimeSpan.FromSeconds(reloadTimeout), cancellationToken);

                //var completedTask = await Task.WhenAny(reloadTask, timeoutTask);
                //if (completedTask == timeoutTask)
                //{
                //    _logger.Warning("{TITLE} warning: {SETTINGKEY} reloading skipped due to timeout: ({RELOADTIMEOUT}) and it will repeat at next time: {RELOADINTERVAL}",
                //        ApiHelper.LogTitle(), reloadQueueItem.SettingKey, reloadTimeout, reloadInterval);
                //}

                //if (!factory.SettingsTaskReloadQueue.Contains(reloadQueueItem.SettingKey))
                //    factory.SettingsTaskReloadQueue.EnqueueTask(reloadQueueItem.SettingKey, factory.MakeReloadTask(reloadQueueItem.SettingKey));
            }
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
                //var serviceFactory = _serviceProvider.GetService<IApiConfigurationFactory>()!;
                //serviceFactory.ReloadTaskQueue.Items.Clear();

                await base.StopAsync(cancellationToken);

                _logger.Information("ExrenalSettingsHostService stoped.");
            }
            catch (Exception ex)
            {
                var errMsg = ex.InnerException?.Message ?? ex.Message;
                Log.Logger.Error("{TITLE} failed: {MESSAGE}", ApiHelper.LogTitle(), errMsg);
            }
        }

        public void SetActive(bool active)
        {
            IsActive = active;

            if (!IsActive)
                _logger.Warning($"{nameof(ReloadWorkerService)} reloading stopped. For start, let call to: SetActive(true)");
        }

        #endregion public methods

        #region private methods

        private void ShowConfigrations()
        {
            _logger.Debug(_configuration[SettingKeys.StaticData.ToString()]!);
        }

        private void UpdateRegistration()
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
