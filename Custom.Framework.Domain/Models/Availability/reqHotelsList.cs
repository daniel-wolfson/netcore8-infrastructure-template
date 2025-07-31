using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Custom.Domain.Optima.Models.Availability
{
    public class ReqHotelsList
    {
        [JsonProperty("hotelID")]
        public int? HotelID { get; set; }

        [JsonProperty("regionID")]
        public int? RegionID { get; set; }

        [JsonProperty("grade")]
        public int? Grade { get; set; }

        [JsonProperty("ChainID")]
        public int? ChainID { get; set; }

        [JsonProperty("cityCode")]
        public string? CityCode { get; set; }

        [JsonProperty("countryCode")]
        public string? CountryCode { get; set; }

        [JsonProperty("primaryHotelID")]
        public int? PrimaryHotelID { get; set; }

        [JsonProperty("wing")]
        public string? Wing { get; set; }

        [JsonProperty("ignoreHeartBeatCheck")]
        public bool IgnoreHeartBeatCheck { get; set; }

        [JsonProperty("disableCache")]
        public bool DisableCache { get; set; }

        //[JsonProperty("userName")]
        //public string UserName { get; set; }

        //[JsonProperty("password")]
        //public string Password { get; set; }

        [JsonProperty("customerID")]
        public int? CustomerID { get; set; }

        [JsonProperty("salesChannelID")]
        public int? SalesChannelID { get; set; }

        [JsonProperty("internalUse")]
        public string? InternalUse { get; set; }

        [JsonProperty("requestUniqueID")]
        public string? RequestUniqueID { get; set; }

        [JsonProperty("clientType")]
        public int? ClientType { get; set; }

        [JsonProperty("ignoreClientError")]
        public bool IgnoreClientError { get; set; }

        [JsonProperty("customerGroupID")]
        public int? CustomerGroupID { get; set; }

        [JsonProperty("origCustomerID")]
        public int? OrigCustomerID { get; set; }

        [JsonProperty("refreshCache")]
        public bool RefreshCache { get; set; }

        [JsonProperty("machineName")]
        public string? MachineName { get; set; }
    }
}