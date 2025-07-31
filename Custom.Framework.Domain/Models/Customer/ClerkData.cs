using Custom.Domain.Optima.Models.Base;
using Newtonsoft.Json;

namespace Custom.Domain.Optima.Models.Customer
{
    public class ClerkData : OptimaData
    {
        [JsonProperty("webPriceCode")]
        public string? WebPriceCode { get; set; }

        [JsonProperty("promoCode")]
        public string? PromoCode { get; set; }

        [JsonProperty("clerkKey")]
        public required string ClerkKey { get; set; }

        [JsonProperty("hotelID")]
        public int HotelID { get; set; }

        [JsonProperty("creditType")]
        public int CreditType { get; set; }

        [JsonProperty("inBlackList")]
        public bool InBlackList { get; set; }

        [JsonProperty("bookingComments")]
        public string? BookingComments { get; set; }

        [JsonProperty("isShowBookingCommentsOnWeb")]
        public bool IsShowBookingCommentsOnWeb { get; set; }

        [JsonProperty("allocationName")]
        public string? AllocationName { get; set; }

        [JsonProperty("firstName")]
        public string? FirstName { get; set; }

        [JsonProperty("lastName")]
        public string? LastName { get; set; }

        [JsonProperty("firstName2")]
        public string? FirstName2 { get; set; }

        [JsonProperty("lastName2")]
        public string? LastName2 { get; set; }

        [JsonProperty("phone")]
        public string? Phone { get; set; }

        [JsonProperty("phone2")]
        public string? Phone2 { get; set; }

        [JsonProperty("faxNumber")]
        public string? FaxNumber { get; set; }

        [JsonProperty("emailAddress")]
        public string? EmailAddress { get; set; }

        [JsonProperty("webPassword")]
        public string? WebPassword { get; set; }

        [JsonProperty("lastCrmDate")]
        public DateTime LastCrmDate { get; set; }

        [JsonProperty("timeStamp")]
        public DateTime TimeStamp { get; set; }
    }
}
