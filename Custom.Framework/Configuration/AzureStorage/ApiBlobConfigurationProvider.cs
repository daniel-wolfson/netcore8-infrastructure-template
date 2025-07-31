using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Custom.Framework.Configuration.AzureStorage
{
    /// <summary>
    /// ApiBlobConfigurationProvider - retrieves the configuration data from Azure Blob Storage.
    /// </summary>
    public class ApiBlobConfigurationProvider(IServiceScopeFactory serviceScopeFactory, ApiConfigurationSource source)
        : ApiConfigurationProvider(serviceScopeFactory, source), IConfigurationProvider
    {
        /// <summary>
        /// Retrieves the configuration data asynchronously for the specified setting key.
        /// </summary>
        public override async Task<IServiceResult<TData>> GetAsync<TData>(SettingKeys settingKey, CancellationToken cancelToken = default)
        {
            try
            {
                // Call the base GetAsync2 method to retrieve the configuration data
                var serviceResult = await base.GetAsync<TData>(settingKey, cancelToken);

                if (serviceResult.IsSuccess && !ApiHelper.IsDataNullOrEmpty(serviceResult.Value))
                {
                    return serviceResult;
                }
                else if (serviceResult.Value == null)
                {
                    var blobName = $"{AppSettings.Version}/{settingKey}.json";
                    var containerName = AppSettings.AzureStorage.ContainerName;
                    var errMsg = $"{ApiHelper.LogTitle()} error: blob name {blobName} returns null";

                    Logger.Error("{TITLE} error: blob name {blobName} returns null",
                        ApiHelper.LogTitle(), containerName, blobName);

                    return ServiceResult<TData>.Error(errMsg);
                }

                return ServiceResult<TData>.Ok(serviceResult.Value);
            }
            catch (Exception ex)
            {
                var errMsg = ex.InnerException?.Message ?? ex.Message;

                Logger.Error("{TITLE} error: get key {KEY} failed. Exception: {ERROR}. \nStackTrace: {STACKTRACE}\n",
                       ApiHelper.LogTitle(), settingKey, errMsg, ex.StackTrace);

                return ServiceResult<TData>.Error(errMsg, typeof(TData).GetDefault<TData>());
            }
        }
    }
}

