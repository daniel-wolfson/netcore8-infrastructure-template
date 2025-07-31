using Newtonsoft.Json;

namespace Custom.Domain.Optima.Models.Availability
{
    public class RoomsList
    {
        [JsonProperty("requestIndex")]
        public int? RequestIndex { get; set; }

        [JsonProperty("hotelID")]
        public int? HotelID { get; set; }

        [JsonProperty("FromDate")]
        public string FromDate { get; set; }

        //[JsonProperty("fromDateObj")]
        //public FromDateObj? FromDateObj { get; set; }

        [JsonProperty("nights")]
        public int Nights { get; set; }

        [JsonProperty("isLocal")]
        public bool IsLocal { get; set; } = true;

        [JsonProperty("roomCategory")]
        public string? RoomCategory { get; set; }

        [JsonProperty("planCode")]
        public string? PlanCode { get; set; }

        [JsonProperty("priceCode")]
        public string? PriceCode { get; set; }

        [JsonProperty("adults")]
        public int Adults { get; set; }

        [JsonProperty("children")]
        public int Children { get; set; }

        [JsonProperty("infants")]
        public int Infants { get; set; }

        [JsonProperty("languageID")]
        public int LanguageID { get; set; } = 1;

        [JsonProperty("guestID")]
        public int? GuestID { get; set; }

        [JsonProperty("reasonCode")]
        public string? ReasonCode { get; set; }

        [JsonProperty("clubCode")]
        public string? ClubCode { get; set; }

        [JsonProperty("showErrors")]
        public bool ShowErrors { get; set; } = true;

        [JsonProperty("disableCacheSearch")]
        public bool DisableCacheSearch { get; set; }

        [JsonProperty("minRoomAvailabiltyToUseCache")]
        public int? MinRoomAvailabiltyToUseCache { get; set; }

        [JsonProperty("requestDescriptor")]
        public int? RequestDescriptor { get; set; }

        [JsonProperty("decimals")]
        public int? Decimals { get; set; } = 2;

        [JsonProperty("reqDefinitions")]
        public ReqDefinitions? ReqDefinitions { get; set; }

        [JsonProperty("ignoreCouchBaseCacheOnlyMode")]
        public bool IgnoreCouchBaseCacheOnlyMode { get; set; }

        [JsonProperty("doNotAddResultToMongo")]
        public bool DoNotAddResultToMongo { get; set; }

        [JsonProperty("hotelDefCrncy")]
        public string? HotelDefCrncy { get; set; }

        [JsonProperty("promoCode")]
        public string? PromoCode { get; set; }

        [JsonProperty("userName")]
        public string UserName { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("customerID")]
        public int? CustomerID { get; set; }

        [JsonProperty("salesChannelID")]
        public int? SalesChannelID { get; set; }

        [JsonProperty("internalUse")]
        public string InternalUse { get; set; }

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