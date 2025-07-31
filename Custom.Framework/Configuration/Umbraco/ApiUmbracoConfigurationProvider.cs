using Custom.Framework.Configuration.Models;
using Custom.Framework.Configuration.Optima;
using Custom.Framework.Contracts;
using Custom.Framework.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Custom.Framework.Configuration.Umbraco
{
    public class ApiUmbracoConfigurationProvider(IServiceScopeFactory serviceScopeFactory, ApiConfigurationSource source)
        : ApiConfigurationProvider(serviceScopeFactory, source), IConfigurationProvider
    {
        /// <summary> Download blob data from azure store </summary>
        public override async Task<IServiceResult<TData>> GetAsync<TData>(
            SettingKeys settingKey, CancellationToken cancelToken = default)
        {
            var serviceResult = await base.GetAsync<TData>(settingKey, cancelToken);
            if (serviceResult.IsSuccess && !ApiHelper.IsDataNullOrEmpty(serviceResult.Value))
                return serviceResult;

            var scope = ServiceScopeFactory.CreateScope();
            var umbracoApi = scope.ServiceProvider.GetService<IUmbracoApi>()!;
            var result = await umbracoApi.GetAsync<TData>(settingKey, cancelToken);
            return result;
        }

        protected override T? ParseContent<T>(SettingKeys settingsKey, string content) where T : default
        {
            try
            {
                switch (settingsKey)
                {
                    case SettingKeys.UmbracoSettings:
                        var allSitesSettings = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
                        if (allSitesSettings != null)
                        {
                            var umbracoSearchSettingsData = allSitesSettings.ToDictionary(
                                site => int.Parse(site.Key),
                                site => JsonConvert.DeserializeObject<UmbracoSettings>(site.Value));

                            return (T?)Convert.ChangeType(umbracoSearchSettingsData, typeof(T?));
                        }
                        else
                        {
                            Logger.Error("{TITLE} error: {KEY} parsing failed", $"{ApiHelper.LogTitle()}.{nameof(GetAsync)}", settingsKey);
                            throw new NotImplementedException($"{settingsKey} parsing failed");
                        }
                    case SettingKeys.OptimaSettings:
                        var optimaSettings = JsonConvert.DeserializeObject<OptimaSettings>(content);
                        if (optimaSettings != null)
                            return (T)(object)optimaSettings;

                        else
                        {
                            Logger.Error("{TITLE} error: {KEY} parsing failed", $"{ApiHelper.LogTitle()}.{nameof(GetAsync)}", settingsKey);
                            throw new NotImplementedException($"{settingsKey} parsing failed");
                        }
                    default:
                        var data = base.ParseContent<T>(settingsKey, content);
                        return data;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                var data = typeof(T?).GetDefault<T?>();
                return data;
            }
        }
    }
}

