using Custom.Framework.Models.Base;

namespace Custom.Framework.Models.Main
{
    public class TranslationsPackageRequest : OptimaRequest
    {
        public int LanguageID { get; set; }
        public int? PackageID { get; set; }
        public int HotelID { get; set; }
        public int SalesChannelID { get; set; }
        public string InternalUse { get; set; }
        public string RequestUniqueID { get; set; }
        public int ClientType { get; set; }
        public bool IgnoreClientError { get; set; }
        public int CustomerGroupID { get; set; }
        public bool RefreshCache { get; set; }
        public string MachineName { get; set; }
    }
}