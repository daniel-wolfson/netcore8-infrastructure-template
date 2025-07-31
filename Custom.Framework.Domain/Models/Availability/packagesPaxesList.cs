using Newtonsoft.Json;

namespace Custom.Domain.Optima.Models.Availability
{
    public class PackagesPaxesList
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? HotelID { get; set; }
        public string FromDate { get; set; }
        public string ToDate { get; set; }
        public int Adults { get; set; }
        public int Children { get; set; }
        public int Infants { get; set; }
        public int LanguageID { get; set; } = 1;
        public bool ShowErrors { get; set; } = true;
        public int Decimals { get; set; } = 2;
        public bool IncludeDerivedPackages { get; set; } = true;
        public int? CustomerID { get; set; }
        public bool IsLocal { get; set; } = true;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? PromoCode { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? CityCode { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? ClubCode { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? ReasonCode { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? GuestId { get; set; }
    }
}