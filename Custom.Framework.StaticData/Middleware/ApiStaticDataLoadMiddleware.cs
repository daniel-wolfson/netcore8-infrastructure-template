using Custom.Framework.Configuration;
using Custom.Framework.Extensions;
using Custom.Framework.Helpers;
using Custom.Framework.StaticData.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Serilog;

namespace Custom.Framework.StaticData.Middleware
{
    public class ApiStaticDataLoadMiddleware(string providerType, RequestDelegate next,
        IStaticDataService staticDataService, IOptions<ApiSettings> apiSettingsOptions,
        ILogger logger)
    {
        private static int _activeRequests = 0;
        private readonly RequestDelegate _next = next;
        private readonly ApiSettings _apiSettings = apiSettingsOptions.Value;

        public async Task Invoke(HttpContext context)
        {
            await HandleLoadData(context, providerType);
            await _next.Invoke(context);
        }

        private async Task HandleLoadData(HttpContext context, string providerType)
        {
            try
            {
                staticDataService.UseMemoryCache =
                    context.Request.Query.TryGetValue("UseMemoryCache", out var value) == true
                    && bool.TryParse(value, out var memoryCacheEnabled)
                    ? memoryCacheEnabled : _apiSettings.Optima.UseMemoryCache;

                if (staticDataService.UndefinedStaticData.Count != 0)
                {
                    var settingKeys = staticDataService.UndefinedStaticData;

                    await staticDataService.LoadStaticData(providerType, settingKeys);

                    if (staticDataService.UseMemoryCache)
                        staticDataService.SetToMemoryCache(settingKeys);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "{TITLE} An unexpected exception occured!. Path: {PATH}. StackTrace: {STACKTRACE}",
                    ApiHelper.LogTitle(), context.GetRequestFullPath(), ex.StackTrace);
            }
        }
    }
}
