using Custom.Domain.Optima.Models.Umbraco;

namespace Custom.Domain.Optima.Models.Nop
{
    public class NopSearchSettings
    {
        public Dictionary<string, decimal> Discounts { get; set; }
        /// <summary>
        /// Defines the discount amount for flights (only for sun club)
        /// </summary>
        public decimal SunClubFlightDiscount { get; set; }
        /// <summary>
        /// Max allowed Price change limit for MAIN site
        /// </summary>
        public decimal EdgeCasePercentRange { get; set; }
        /// <summary>
        /// Max allowed Price change limit for Agents site
        /// </summary>
        public decimal EdgeCasePercentRangeForAgents { get; set; }
        /// <summary>
        /// For property filter
        /// </summary>
        public List<AvailableProperties> AvailableHotelsAndRooms { get; set; }

        public List<DiscountPackageCode> DiscountsPackageCodes { get; set; }
    }
}