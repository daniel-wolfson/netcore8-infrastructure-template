using Custom.Domain.Optima.Models.Base;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Domain.Optima.Models.Main
{
    public class PriceCodeData : OptimaData
    {
        public int HotelID { get; set; }
        public string PriceCode { get; set; }
        public string InternetPriceCode { get; set; }
        public bool IsPromoCodeOnly { get; set; }
        public bool IsOverRideClubDiscount { get; set; }
        public decimal DiscountPercent { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }
        public string ReasonCode { get; set; }
        public string OtaRateType { get; set; }
        public bool IsLocal { get; set; }
        public int NotAllowedForCustomerGroupID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PolicyCode { get; set; }
        public int DefaultLeadDays { get; set; }
        public int DefaultCancelDays { get; set; }
        public decimal? CommissionPercent { get; set; }
        public bool IsNetPrice { get; set; }
        public string? PriceGroupId { get; set; }

        public List<string>? PromoCodesList { get; set; }
        /// <summary>
        /// Should be promoted in calendar
        /// </summary>
        public bool AddToCalendar => ShouldAddToCalendar();
        public string Stamp => GetStamp();

        private bool ShouldAddToCalendar()
        {
            return GenericParameters?.TryGetValue("addToCalander", out var value) == true &&
                   value.Equals("Y", StringComparison.OrdinalIgnoreCase);
        }

        private string GetStamp()
        {
            return GenericParameters?.TryGetValue("stamp", out var value) == true
                ? value
                : string.Empty;
        }
    }
}