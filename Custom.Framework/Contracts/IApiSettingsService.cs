using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Custom.Framework.Contracts
{
    public interface IApiSettingsService_
    {
        T? Get<T>(SettingKeys requestedSettings);

        Task<T?> GetAsync<T>(SettingKeys requestedSettings);

        Task<bool> SetAsync<T>(string blobName, T data);

        ApiSettings? GetAll(ApiSettings? _apiSettings);

        Task<ApiSettings?> GetAllAsync();
    }
}