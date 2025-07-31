using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;

namespace Custom.Framework.Configuration.Optima
{
    public class ApiOptimaConfigurationProvider(IServiceScopeFactory serviceScopeFactory, ApiConfigurationSource source)
        : ApiConfigurationProvider(serviceScopeFactory, source), IConfigurationProvider
    {
        /// <summary> GetAsync2 blob data from azure store </summary>
        public override async Task<IServiceResult<TData>> GetAsync<TData>(SettingKeys settingKey, CancellationToken cancelToken = default)
        {
            // GetAsync2 => from RedisCache or from BlobStorage
            var dataResult = await base.GetAsync<TData>(settingKey, cancelToken);

            // dataResult may be successful and have status not equals to 200, for example - 204 - no content
            if (dataResult.IsSuccess && dataResult.Status == 200)
                return dataResult;

            var customerIds = source.GetConfiguration()
                .GetSections(SettingKeys.StaticData)
                .FirstOrDefault(x => x.SettingKey == SettingKeys.CustomerIds)?
                .Value as List<int> ?? [];

            // optimaMainApi GetAsync2 data from optima
            using var scope = ServiceScopeFactory.CreateScope();
            var optimaMainApi = scope.ServiceProvider.GetService<IStaticDataRepository>()!;
            var optimaResult = await optimaMainApi.GetAsync<TData>(settingKey, cancelToken);

            if (optimaResult.Data != null)
                return ServiceResult<TData>.Ok(optimaResult.Data ?? settingKey.GetDefault<TData>());
            else
                return ServiceResult<TData>.Default(settingKey.GetDefault<TData>());
        }

        public override async Task<IServiceResult<object>> LoadAsync(SettingKeys settingKey, EReasonTypes reason, CancellationToken cancelToken = default)
        {
            var scope = ServiceScopeFactory.CreateScope();
            var optimaApi = scope.ServiceProvider.GetService<IStaticDataRepository>()!;
            var result = await optimaApi.ReadAsync<object>(settingKey, cancelToken);
            return ServiceResult<object>.Ok(result.Data);
        }
    }
}

