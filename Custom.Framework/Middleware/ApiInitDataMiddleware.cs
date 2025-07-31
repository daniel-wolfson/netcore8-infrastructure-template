using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Exceptions;
using Custom.Framework.Extensions;
using Custom.Framework.Models;
using Custom.Framework.StaticData.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Custom.Framework.Middleware
{
    public class ApiInitDataMiddleware
    {
        private readonly RequestDelegate _next;
        private Task? _initializationTask;
        private IApiConfigurationFactory _apiConfigurationFactory;
        private IServiceProvider _serviceProvider;
        private IApiCacheOptions _apiMemoryCacheOptions;
        private IHttpContextAccessor _httpContextAccessor;
        private IHostApplicationLifetime _lifetime;
        private bool _usePreload;


        public ApiInitDataMiddleware(bool usePreload,
            RequestDelegate next,
            IHttpContextAccessor httpContextAccessor,
            IHostApplicationLifetime lifetime,
            IServiceProvider serviceProvider,
            IApiCacheOptions apiMemoryCacheOptions)
        {
            _usePreload = usePreload;
            _next = next;
            _httpContextAccessor = httpContextAccessor;
            _lifetime = lifetime;
            _serviceProvider = serviceProvider;
            _apiMemoryCacheOptions = apiMemoryCacheOptions;

            // Start initialization when the app starts
            var startRegistration = default(CancellationTokenRegistration);

            if (_usePreload)
                startRegistration = _lifetime.ApplicationStarted.Register(() =>
                {
                    _initializationTask = Task.Run(async () =>
                    {
                        await InitializeAsync();
                    });
                    startRegistration.Dispose();
                });
        }

        public async Task Invoke(HttpContext context)
        {
            // Take a copy to avoid race conditions
            var initializationTask = _initializationTask;

            if (initializationTask != null) // initializationTask != null
            {
                using (var scope = context.RequestServices.CreateScope())
                {
                    _apiConfigurationFactory = scope.ServiceProvider.GetService<IApiConfigurationFactory>()!;

                    // Wait until initialization is complete before passing the request to next middleware
                    await initializationTask;

                    // Clear the task so that we don't await it again later.
                    _initializationTask = null;
                }
            }
            else if (context.GetRequestHeader(RequestHeaderKeys.UpdateSettingsCache) != null)
            {
                await InitializeAsync();
            }

            // Pass the request to the next middleware
            await _next(context);
        }

        private async Task InitializeAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var staticDataService = scope.ServiceProvider.GetService<IStaticDataService>()!;
                await staticDataService.LoadStaticData(ProviderTypes.All);
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
