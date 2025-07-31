namespace Custom.Domain.Optima.Models.Availability
{
    public class PolicyInfo
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public decimal Price { get; set; }
        public decimal PriceNoTax { get; set; }
        public PolicyDay PolicyDay { get; set; }
        public List<string> DebugInfo { get; set; }
    }
}