using Custom.Framework.Cache;
using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Dal;
using Custom.Framework.Configuration.Optima;
using Custom.Framework.Configuration.Umbraco;
using Custom.Framework.Telemetry;

namespace Custom.Framework.Contracts
{
    public interface IApiSettings
    {
        public string Version { get; set; }
        public DalConfig Dal { get; set; }
        public OptimaConfig Optima { get; set; }
        public UmbracoConfig Umbraco { get; set; }
        public RedisConfig Redis { get; set; }
        public CurrencyConfig Currency { get; set; }
        public List<ConfigData> StaticData { get; set; }
        public AzureStorageConfig AzureStorage { get; set; }
        public OpenTelemetryConfig OpenTelemetry { get; set; }
        public string? EmptyTextReplacement { get; set; }
    }
}