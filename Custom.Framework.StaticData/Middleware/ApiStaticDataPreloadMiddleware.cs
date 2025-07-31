using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Exceptions;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Custom.Framework.StaticData.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Custom.Framework.Middleware
{
    public class ApiStaticDataPreloadMiddleware

    {
        private readonly RequestDelegate _next;
        private Task? _initializationTask;
        private IServiceProvider _serviceProvider;
        private IApiCacheOptions _apiMemoryCacheOptions;
        private IHttpContextAccessor _httpContextAccessor;
        private TaskCompletionSource<bool> _taskCompletionSource;
        private IHostApplicationLifetime _lifetime;
        private IStaticDataService _staticData;

        public ApiStaticDataPreloadMiddleware(
            RequestDelegate next,
            IHttpContextAccessor httpContextAccessor,
            IHostApplicationLifetime lifetime,
            IServiceProvider serviceProvider,
            IStaticDataService staticData,
            IApiCacheOptions apiMemoryCacheOptions)
        {
            _next = next;
            _httpContextAccessor = httpContextAccessor;
            _lifetime = lifetime;
            _serviceProvider = serviceProvider;
            _apiMemoryCacheOptions = apiMemoryCacheOptions;
            _taskCompletionSource = new TaskCompletionSource<bool>();
            _taskCompletionSource.SetResult(false);

            // Start initialization when the app starts
            var startRegistration = default(CancellationTokenRegistration);
            startRegistration = _lifetime.ApplicationStarted.Register(() =>
            {
                _taskCompletionSource = new TaskCompletionSource<bool>();

                var requestTask = Task.Run(async () =>
                {
                    await InitializeAsync(lifetime.ApplicationStopping);
                    return true;
                });

                _initializationTask = requestTask;

                startRegistration.Dispose();
            });
        }

        public async Task Invoke(HttpContext context)
        {
            // Take a copy to avoid race conditions
            var initializationTask = _initializationTask;

            if (initializationTask != null)
            {
                // Wait until initialization is complete before passing the request to next middleware
                await initializationTask;

                // Clear the task so that we don't await it again later.
                _initializationTask = null;
            }

            // Pass the request to the next middleware
            await _next(context);
        }

        private async Task InitializeAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var token = new CancellationTokenSource().Token;
                var factory = scope.ServiceProvider.GetService<IApiConfigurationFactory>()!;
                await factory.InitConfigurationSources(SettingKeys.StaticData, token);

                var configuration = scope.ServiceProvider.GetService<IConfiguration>()!;
                var hotels = _staticData.DataContext.FirstOrDefault(x => x.SettingKey == SettingKeys.Hotels);
                if (ApiHelper.IsDataNullOrEmpty(hotels))
                {
                    var staticDataService = scope.ServiceProvider.GetService<IStaticDataService>()!;
                    //((ApiServiceBase)staticData).ServiceScope = scope;
                    //await staticData.LoadStaticData(ProviderTypes.Optima);
                    if (ApiHelper.ServiceName.Contains("StaticDataAndSettings"))
                        await staticDataService.PreloadStaticData(ProviderTypes.Optima);
                    else
                        await staticDataService.LoadStaticData(ProviderTypes.Optima);
                }

                //await Task.Delay(1000);
                _taskCompletionSource.SetResult(true);
            }
            catch (Exception ex)
            {
                throw new ApiException(ServiceStatus.FatalError,
                    $"Initialization failed: {ex.InnerException?.Message ?? ex.Message ?? "error"}", ex);
            }
            //return Task.CompletedTask;
        }
    }
}
