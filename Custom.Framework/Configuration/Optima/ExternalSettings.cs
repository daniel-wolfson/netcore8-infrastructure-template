using Custom.Framework.Enums;

namespace Custom.Framework.Configuration.Optima
{
    public class ExternalSettings(int id, string name, int reloadInterval = 3600) : Enumeration(id, name)
    {
        private static readonly ExternalSettings externalSettings = new(1, nameof(CurrencyRates));
        public static ExternalSettings CurrencyRates = externalSettings;
        public static ExternalSettings OptimaSettings = new(2, nameof(OptimaSettings));
        public static ExternalSettings UmbracoSearchSettings = new(3, nameof(UmbracoSearchSettings));
        public static ExternalSettings AvailabilityPackageShowSettings = new(5, nameof(UmbracoSearchSettings));
        public static ExternalSettings Rooms = new(6, nameof(UmbracoSearchSettings));
        public static ExternalSettings TranslatePackageSettings = new(7, nameof(UmbracoSearchSettings));
        public static ExternalSettings PlansSettings = new(8, nameof(UmbracoSearchSettings));
        public static ExternalSettings PricecodeSettings = new(8, nameof(PricecodeSettings));

        public string SourceType { get; set; }
        public string ConnectionStringRef { get; set; }
        public string DependsOn { get; set; }
        List<string> UniqueKeys { get; set; }
        public string Url { get; set; }
        public int TTL { get; set; }
        public string Path { get; set; }
        public int ReloadInterval { get; set; } = reloadInterval;
        public int ReloadTimeout { get; set; } = 30; //sec
        public bool ReloadOnChange { get; set; }
        public string HotelCodes { get; set; }
    }
}