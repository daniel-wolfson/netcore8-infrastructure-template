using Custom.Framework.Configuration.Models;

namespace Custom.Framework.Contracts
{
    public interface IApiConfigurationFactory
    {
        Task InitConfigurationSources(SettingKeys rootSectionSettingKey, CancellationToken? cancelToken = null);
    }
}