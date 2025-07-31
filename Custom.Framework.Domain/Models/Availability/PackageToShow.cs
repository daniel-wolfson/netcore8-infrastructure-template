namespace Custom.Domain.Optima.Models.Availability
{
    public class PackageToShow : Package
    {
        public bool isPreferredPackage { get; set; }
        /// <summary>
        ///
        /// </summary>
        public string comparisonPriceCode { get; set; }
        /// <summary>
        /// Display Package/Promotion start date
        /// </summary>
        public DateTime displayStartDate { get; set; }
        /// <summary>
        /// Display Package/Promotion end date
        /// </summary>
        public DateTime displayEndDate { get; set; }
        /// <summary>
        /// Package/Promotion minimum length of stay
        /// </summary>
        public short minLOS { get; set; }
        /// <summary>
        /// Package/Promotion maximum length of stay
        /// </summary>
        public short maxLOS { get; set; }
        /// <summary>
        /// Package/Promotion valid days: Sunday-Saturday
        /// </summary>
        public bool validOnSun { get; set; }
        public bool validOnMon { get; set; }
        public bool validOnTue { get; set; }
        public bool validOnWed { get; set; }
        public bool validOnThu { get; set; }
        public bool validOnFri { get; set; }
        public bool validOnSat { get; set; }
        /// <summary>
        /// Package/Promotion arrive days: Sunday-Saturday
        /// </summary>
        public bool arriveOnSun { get; set; }
        public bool arriveOnMon { get; set; }
        public bool arriveOnTue { get; set; }
        public bool arriveOnWed { get; set; }
        public bool arriveOnThu { get; set; }
        public bool arriveOnFri { get; set; }
        public bool arriveOnSat { get; set; }
        /// <summary>
        /// Package/Promotion display order
        /// </summary>
        public int displayOrder { get; set; }
        /// <summary>
        /// wing code
        /// </summary>
        public string wing { get; set; }
        /// <summary>
        /// package picture URL
        /// </summary>
        public string picture1Url { get; set; }
        /// <summary>
        /// package picture URL
        /// </summary>
        public string picture2Url { get; set; }
        /// <summary>
        /// The price after agent discount
        /// </summary>
        public decimal priceAfterAgentDiscount { get; set; }
        /// <summary>
        /// added to v7.0.0.0
        /// Price without tax (vat).
        /// the tax (vat) will be removed only if it exists in the equivalent field.
        /// the tax (vat) in being added only for products that their currency code is
        /// equal to the default currency of the hotel and include vat field (get hotels)
        /// in on, otherwise the NoTax field is equal to the equivalent field.
        /// </summary>
        public decimal priceAfterAgentDiscountNoTax { get; set; }
        /// <summary>
        /// price after x% user discount Percentage - for example to reduce Availability 
        /// discount from the Total Price.
        /// </summary>
        public decimal priceAfterUserDiscount { get; set; }
        /// <summary>
        /// added to v7.0.0.0
        /// Price without tax (vat).
        /// the tax (vat) will be removed only if it exists in the equivalent field.
        /// the tax (vat) in being added only for products that their currency code is
        /// equal to the default currency of the hotel and include vat field (get hotels)
        /// in on, otherwise the NoTax field is equal to the equivalent field.
        /// </summary>
        public decimal priceAfterUserDiscountNoTax { get; set; }
        /// <summary>
        /// Package is active from startTime
        /// </summary>
        public TimeSpan startTime { get; set; }
        /// <summary>
        /// Package is active to endTime
        /// </summary>
        public TimeSpan endTime { get; set; }

    }
}
