using Custom.Framework.Configuration.Models;

namespace Custom.Framework.Contracts
{
    public interface IUmbracoApi
    {
        Task<IServiceResult<TData>> GetAsync<TData>(SettingKeys settingKey, CancellationToken cancelToken = default);
        Task<IServiceResult<TData>> ReadAsync<TData>(SettingKeys settingKey, CancellationToken cancellationToken = default);
        Task<IServiceResult<bool>> UploadToBlobAsync(SettingKeys settingKey, CancellationToken cancelToken = default);
    }
}