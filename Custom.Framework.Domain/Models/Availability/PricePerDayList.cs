namespace Custom.Domain.Optima.Models.Availability
{
    public class PricePerDayList
    {
        public DateTime DayDate { get; set; }
        public decimal BasePrice { get; set; }
        public decimal BasePriceNoTax { get; set; }
        public int AvailableRooms { get; set; }
        public int? RoomsFromAllocation { get; set; }
        public decimal Discount { get; set; }
        public decimal AvailabilityDiscount { get; set; }
        public decimal PriceAfterDiscount { get; set; }
        public decimal PriceAfterDiscountNoTax { get; set; }
        public decimal PriceAfterAgentDiscount { get; set; }
        public decimal PriceAfterAgentDiscountNoTax { get; set; }
        public decimal PriceAfterClubMemberDiscount { get; set; }
        public decimal PriceAfterClubMemberDiscountNoTax { get; set; }
        public decimal DayFinalPrice { get; set; }
        public decimal DayFinalPriceNoTax { get; set; }
        public decimal Points { get; set; }
        public int PointsChargeType { get; set; }
        public decimal ExpectedPoints { get; set; }
        public decimal PriceAfterInternetDiscount { get; set; }
        public decimal OrigAvDiscount { get; set; }
        public decimal DailyChildPrice { get; set; }
        public decimal DailyInfantPrice { get; set; }
        public decimal InternetDiscRatio { get; set; }
        public List<string> DebugInfo { get; set; }
        public decimal PriceAfterInternetDiscountNoTax { get; set; }
        public decimal PriceAfterAddOnDiscount { get; set; }
        public decimal PriceAfterAddOnDiscountNoTax { get; set; }
        public DerivedPackageDetailsPerDay DerivedPackageDetailsPerDay { get; set; }
        public ExternalSystemCalcMessage ExternalSystemCalcMessage { get; set; }
        public PointsInfo PointsInfo { get; set; }
        public ExpectedPointsInfo ExpectedPointsInfo { get; set; }
        public List<PointsInfoErrorMessage> PointsInfoErrorMessage { get; set; }
        public List<ExpectedPointsInfoErrorMessage> ExpectedPointsInfoErrorMessage { get; set; }
    }
}