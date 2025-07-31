using Custom.Domain.Optima.Models.Base;
using Custom.Domain.Optima.Models.Umbraco;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Domain.Optima.Models.Main
{
    public class HotelData : OptimaData
    {
        public int ParallelOption { get; set; }
        public string Wing { get; set; }
        public int HotelID { get; set; }
        public string HotelCode { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public int ChainID { get; set; }
        public int Grade { get; set; }
        public int DisplaySortOrder { get; set; }
        public string CityCode { get; set; }
        public int RegionID { get; set; }
        public string CountryCode { get; set; }
        public decimal VatPercent { get; set; }
        public bool VatIncluded { get; set; }
        public string DefaultCurrencyCode { get; set; }
        public int PrimaryHotelID { get; set; }
        public string PmsHotelCode { get; set; }
        public string PlanCode { get; set; }
        public string RoomCategory { get; set; }
        public int ActualPrimaryHotelID { get; set; }
        public int NoneGuaranteedLeadDays { get; set; }
        public int FromDateValidHours { get; set; }
        public int MaxAvailableRooms { get; set; }
        public int MaxRoomsAvailabiltyInRequest { get; set; }
        public bool IgnorePriceCodeMinLosForPackages { get; set; }
        public bool AllowRestrictionsForPackages { get; set; }
        public bool IgnoreMinLosForTourists { get; set; }
        public bool DiscountIfTourist { get; set; }
        public bool OverrideSaturdayPriceForTourists { get; set; }
        public bool OverrideShabbatPriceOnFridayArrival { get; set; }

        public virtual List<RoomData> Rooms { get; set; }
    }
}