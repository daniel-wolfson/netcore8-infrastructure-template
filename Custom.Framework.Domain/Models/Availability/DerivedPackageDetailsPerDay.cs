namespace Custom.Domain.Optima.Models.Availability
{
    public class DerivedPackageDetailsPerDay
    {
        public int packageID { get; set; }
        public string? generalDiscount { get; set; }
        public string? childrenDiscount { get; set; }
        public decimal? derivedDiscountPercent { get; set; }
        public string? infantDiscount { get; set; }
    }


}