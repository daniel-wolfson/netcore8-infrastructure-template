using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Optima;
using Custom.Framework.Helpers;
using Custom.Framework.StaticData.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Custom.Framework.StaticData;

public class ReloadCacheService : BackgroundService
{
    private DateTime _lastActivityTime = DateTime.UtcNow;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<ReloadCacheService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OptimaConfig _optimaConfig;
    private readonly TimeSpan _cacheMemoryReloadTTL;
    private bool _applicationStarted;

    public ReloadCacheService(ILogger<ReloadCacheService> logger,
                              IServiceScopeFactory scopeFactory,
                              IHostApplicationLifetime appLifetime,
                              IOptions<ApiSettings> apiSettingsOptions)
    {
        _appLifetime = appLifetime;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _optimaConfig = apiSettingsOptions.Value.Optima;
        _cacheMemoryReloadTTL = TimeSpan.FromSeconds(_optimaConfig.CacheMemoryReloadTTL);

        _appLifetime.ApplicationStarted.Register(() =>
        {
            _applicationStarted = true;
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                _logger.LogInformation("{Title} memory cache reloading started by cacheMemoryReloadTTL ({TTL})", 
                    ApiHelper.LogTitle(), _cacheMemoryReloadTTL);

                var startedTime = Stopwatch.GetTimestamp();

                if (!_applicationStarted)
                {
                    // IStaticDataService must be initilized before execute,
                    // that it will resolve the StaticDataService dependenes
                    scope.ServiceProvider.GetRequiredService<IStaticDataService>();
                }

                var backgroundTask = scope.ServiceProvider.GetRequiredService<IReloadCacheTask>();
                await backgroundTask.ExecuteAsync(stoppingToken);

                _lastActivityTime = DateTime.UtcNow;
                var elapsedTime = Stopwatch.GetElapsedTime(startedTime);

                _logger.LogInformation("{Title} finished in {totalSeconds} sec. Next reload at {totalSeconds} sec.",
                    ApiHelper.LogTitle(), elapsedTime.TotalSeconds.ToString("F2"), _cacheMemoryReloadTTL.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            await Task.Delay(_cacheMemoryReloadTTL, stoppingToken);
        }
    }

    private bool IsCacheReloadRequired(bool isCacheReloadRequiredByMaxTTL, bool isCacheReloadRequiredByTTL)
    {
        return !_applicationStarted || isCacheReloadRequiredByTTL || isCacheReloadRequiredByMaxTTL;
    }
}