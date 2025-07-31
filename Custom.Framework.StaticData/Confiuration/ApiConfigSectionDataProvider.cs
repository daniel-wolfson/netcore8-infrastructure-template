using Custom.Framework.Configuration;
using Microsoft.Extensions.Options;

namespace Custom.Framework.StaticData.Confiuration
{
    public class ApiConfigSectionDataProvider
    {
        private readonly IOptionsSnapshot<ConfigData> _optionsSnapshot;

        public ApiConfigSectionDataProvider(IOptionsSnapshot<ConfigData> optionsSnapshot)
        {
            _optionsSnapshot = optionsSnapshot;
        }

        public ConfigData GetOptions(string key)
        {
            // This assumes that the options were configured with named options using the key.
            return _optionsSnapshot.Get(key);
        }
    }
}