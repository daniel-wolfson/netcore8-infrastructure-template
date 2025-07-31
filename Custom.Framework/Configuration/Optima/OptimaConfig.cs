namespace Custom.Framework.Configuration.Optima
{
    public class OptimaConfig : ConfigData
    {
        public string AvailabilityPricesUrl { get; set; }
        public string AvailabilityPackageShowUrl { get; set; }
        public string AvailabilityPackageSessionUrl { get; set; }
        public string AvailabilityCalendarUrl { get; set; }

        public string TranslationsPackageUrl { get; set; }
        
        public string RoomsUrl { get; set; }
        public string PlansUrl { get; set; }
        public string PriceCodeUrl { get; set; }
        public string GetSessionUrl { get; set; }
        public string UpdateRoomSessionUrl { get; set; }
        public string UpdatePackageSessionUrl { get; set; }
        public int CacheMemoryReloadTTL { get; set; } = 3;
        public bool UseMemoryCache { get; set; }
        public string FitPriceGroupId { get; set; }
    }
}