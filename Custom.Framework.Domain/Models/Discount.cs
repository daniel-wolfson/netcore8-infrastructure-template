namespace Custom.Domain.Optima.Models
{
    public class Discount
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool UsePercentage { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal DiscountAmount { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Comments { get; set; }
        public string ImageUrl { get; set; }
        public bool IsWideImage { get; set; }
        //public List<HotelRoomPair> HotelsAndRooms { get; set; }
        //public DiscountPrice DiscountPrice { get; set; }
        public decimal FinalPrice { get; set; }
        public List<string> Tags { get; set; }
        public List<DayOfWeek> AvailableDays { get; set; }
        public int? MinimumDays { get; set; }
        public int? MaximumDays { get; set; }
        public string TermsOfDiscount { get; set; }
        public string TermsOfDiscountTitle { get; set; }
        public bool RemoveFromSearch { get; set; }
        public DateTime? AvalibaleStartDate { get; set; }
        public DateTime? AvalibaleEndDate { get; set; }
        public string ShortTitle { get; set; }
        public string DiscountCondition { get; set; }
        public string ShortDescription { get; set; }
        public string LineTitle { get; set; }
        public string Stamp { get; set; }
        public string OfferLongDescription { get; set; }
        public string FlightTitle { get; set; }
        public string FlightHotelTitle { get; set; }
        public string CouponCode { get; set; }
        public bool HasDynamicFlight { get; set; }
        //public DiscountAssignedType DiscountType { get; set; }
        public bool IsDoubleDiscount { get; set; }

        public bool IsSunClubRquierd { get; set; }

        public string RateCode { get; set; }


        public bool IsNightDiscount { get; set; }
        //public List<DiscountNight> DiscountsPerNight { get; set; }

        public string Airline { get; set; }

        public string FromAirport { get; set; }

        public string FlightType { get; set; }

        public List<string> Channels { get; set; }

        public bool MobileOnly { get; set; }
        public bool isWL { get; set; }

        public bool IsNetPrice { get; set; }

        public decimal? CommissionRate { get; set; }
        public int DiscountFilterCategoryId { get; set; }
        public DiscountType Type { get; set; }
    }
}