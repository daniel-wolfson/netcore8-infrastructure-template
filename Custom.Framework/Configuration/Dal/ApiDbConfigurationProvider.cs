using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Core;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Custom.Framework.Configuration.Dal
{
    public class ApiDbConfigurationProvider(IServiceScopeFactory serviceScopeFactory, ApiConfigurationSource source)
        : ApiConfigurationProvider(serviceScopeFactory, source), IConfigurationProvider
    {
        public override async Task<IServiceResult<TData>>GetAsync<TData>(SettingKeys settingKey, CancellationToken cancelToken = default)
        {
            try
            {
                var serviceResult = await base.GetAsync<TData>(settingKey, cancelToken);
                if (serviceResult.IsSuccess && !ApiHelper.IsDataNullOrEmpty(serviceResult.Value))
                    return serviceResult;

                var settingSectionKey = settingKey == RootSettingKey ? settingKey.ToString() : $"{RootSettingKey}:{settingKey}";
                var options = Configuration.GetSection(settingSectionKey).GetChildren();
                var dalUrl = Configuration.GetValue<string>($"{settingSectionKey}:Host");
                var dalPath = options.FirstOrDefault(x => x.Key == "CurrencyRatesPath")?.Value ?? "";

                var httpClientFactory = GetService<IApiHttpClientFactory>()!;
                using var httpClient = httpClientFactory.CreateClient(ApiHttpClientNames.DalApi);
                var response = await httpClient.GetAsync<TData>(dalPath, cancelToken); //List<CurrencyRate>
                var jsonResult = JsonConvert.SerializeObject(response.Value);
                var objectResult = response.Value;

                if (!response.IsSuccess)
                {
                    var error = response.Message;
                    Logger.Error("{TITLE} {SETTINGSKEY} get failed: {ERROR}. Url: {URL}",
                        ApiHelper.LogTitle(), settingKey, error, httpClient.BaseAddress + dalPath);
                    return new ServiceResult<TData>(error, objectResult);
                    //(objectResult, response.IsSuccess, error);
                }

                return new ServiceResult<TData>(default, objectResult);
                //(objectResult, response.IsSuccess, default);
            }
            catch (Exception ex)
            {
                var errMsg = ex.InnerException?.Message ?? ex.Message;
                Logger.Error("{TITLE} Currency rates response exeption: {ERROR}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), errMsg, ex.StackTrace);
                return new ServiceResult<TData>(errMsg);
                //(default, false, errMsg);
            }
        }
    }
}

