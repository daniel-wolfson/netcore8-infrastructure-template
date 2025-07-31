using Custom.Framework.Cache;
using Custom.Framework.Configuration.Dal;
using Custom.Framework.Configuration.Nop;
using Custom.Framework.Configuration.Optima;
using Custom.Framework.Configuration.Umbraco;
using Custom.Framework.Contracts;
using Custom.Framework.Telemetry;

namespace Custom.Framework.Configuration
{
    public class ApiSettings : IApiSettings
    {
        public required string Version { get; set; }
        public DalConfig Dal { get; set; } = null!;
        public OptimaConfig Optima { get; set; } = null!;
        public UmbracoConfig Umbraco { get; set; } = null!;
        public RedisConfig Redis { get; set; } = null!;
        public CurrencyConfig Currency { get; set; } = null!;
        public List<ConfigData> StaticData { get; set; } = null!;
        public AzureStorageConfig AzureStorage { get; set; } = null!;
        public OpenTelemetryConfig OpenTelemetry { get; set; } = null!;
        public string? EmptyTextReplacement { get; set; } = "...";
        public NopConfig Nop { get; set; }
    }
}