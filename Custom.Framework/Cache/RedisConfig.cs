using Custom.Framework.Configuration;

namespace Custom.Framework.Cache
{
    public class RedisConfig : ConfigData
    {
        /// <summary>
        /// OptimaStaticData RootKey for save all optima external settings data
        /// </summary>
        public CacheKeys StaticDataRootKey { get; set; }

        public int SettingsTTl { get; set; } = 3600; // sec

        public int SalesTTL { get; set; } = 3600; // sec

        public int OnlineReservationTtlMinutes { get; set; } = 1400;

        public int KeepAlive { get; set; } = 180; // sec
        public bool TelemetryTraceEnabled { get; set; } = false;
    }
}
