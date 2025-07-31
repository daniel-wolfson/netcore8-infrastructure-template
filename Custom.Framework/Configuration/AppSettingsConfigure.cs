using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Custom.Framework.Configuration
{
    public class AppSettingsConfigure : IConfigureOptions<ApiSettings>
    {
        private readonly IConfiguration _configuration;

        public AppSettingsConfigure(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Configure(ApiSettings options)
        {
            //options.Version = _configuration.GetSettingValue<string>("Version")
            //    ?? throw new ArgumentNullException("ApiSettings.Version in not defined");
            //options.Optima = _configuration.GetSections("OptimaConfig").Get<OptimaOptions>()
            //    ?? throw new ArgumentNullException("ApiSettings.Optima in not defined");
            //options.Search = _configuration.GetSections("SearchConfig").Get<SearchOptions>()
            //    ?? throw new ArgumentNullException("ApiSettings.Search in not defined");
            //options.Redis = _configuration.GetSections("RedisConfig").Get<RedisOptions>()
            //    ?? throw new ArgumentNullException("ApiSettings.Nop in not defined");
            //options.Currency = _configuration.GetSections("CurrencyConfig").Get<CurrencyOptions>()
            //    ?? throw new ArgumentNullException("ApiSettings.Nop in not defined");
            //options.AzureStorage = _configuration.GetSections("AzureStorageConfig").Get<AzureStorageOptions>()
            //    ?? throw new ArgumentNullException("ApiSettings.AzureStorage in not defined");
        }
    }
}