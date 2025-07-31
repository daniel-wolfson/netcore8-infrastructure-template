namespace Custom.Domain.Optima.Models.Availability
{
    public class Package
    {
        /// <summary>
        /// Package/Promotion hotel id
        /// </summary>
        public int hotelID { get; set; }
        /// <summary>
        /// Package/Promotion id
        /// </summary>
        public long packageID { get; set; }
        /// <summary>
        /// Parent Package/Promotion id - if parentID is zero it’s a parent package, derived 
        /// package are always parent packages
        /// </summary>
        public long parentID { get; set; }
        /// <summary>
        /// Translated Package/Promotion Name
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// Translated Package/Promotion description
        /// </summary>
        public string description { get; set; }
        /// <summary>
        /// /// Translated Package/Promotion short description
        /// </summary>
        public string shortDescription { get; set; }
        /// <summary>
        /// Package/Promotion start date
        /// </summary>
        public DateTime startDate { get; set; }
        /// <summary>
        /// Package/Promotion end date
        /// </summary>
        public DateTime endDate { get; set; }
        /// <summary>
        /// same as totalPrice just without the discounts
        /// </summary>
        public decimal basePrice { get; set; }
        /// <summary>
        /// added to v7.0.0.0
        /// Price without tax (vat).
        /// the tax (vat) will be removed only if it exists in the equivalent field.
        /// the tax (vat) in being added only for products that their currency code is
        /// equal to the default currency of the hotel and include vat field (get hotels)
        /// in on, otherwise the NoTax field is equal to the equivalent field.
        /// </summary>
        public decimal basePriceNoTax { get; set; }
        /// <summary>
        /// Package/Promotion totalPrice(New price) = room type, Price Code, plan combination 
        /// calculation, included discounts, Includes vat only for local price.
        /// </summary>
        public decimal totalPrice { get; set; }
        /// <summary>
        /// added to v7.0.0.0
        /// Price without tax (vat).
        /// the tax (vat) will be removed only if it exists in the equivalent field.
        /// the tax (vat) in being added only for products that their currency code is
        /// equal to the default currency of the hotel and include vat field (get hotels)
        /// in on, otherwise the NoTax field is equal to the equivalent field.
        /// </summary>
        public decimal totalPriceNoTax { get; set; }
        /// <summary>
        /// Package/Promotion old price
        /// in derived packages relevant only to package to show object
        /// </summary>
        public decimal oldPrice { get; set; }
        /// <summary>
        /// added to v7.0.0.0
        /// Price without tax (vat).
        /// the tax (vat) will be removed only if it exists in the equivalent field.
        /// the tax (vat) in being added only for products that their currency code is
        /// equal to the default currency of the hotel and include vat field (get hotels)
        /// in on, otherwise the NoTax field is equal to the equivalent field.
        /// </summary>
        public decimal oldPriceNoTax { get; set; }
        /// <summary>
        /// Currency Code
        /// </summary>
        public string currencyCode { get; set; }
        /// <summary>
        /// Room Category
        /// </summary>
        public string roomCategory { get; set; }
        /// <summary>
        /// Plan code
        /// </summary>
        public string planCode { get; set; }
        /// <summary>
        /// Language ID
        /// </summary>
        public int languageID { get; set; }
        /// <summary>
        /// package category ID list
        /// </summary>
        public List<PackageCategory> categriesList { get; set; }
        /// <summary>
        /// if isDerivedPackage is true this is a derived packages.
        /// derived packages are packages (with only package header) that inherit all their 
        /// pricing, discounts from the price code.
        /// </summary>
        public bool isDerivedPackage { get; set; }

    }
    /// <summary>
    /// Package category
    /// </summary>
    public class PackageCategory
    {
        /// <summary>
        /// Package Category ID
        /// </summary>
        public int packagecategoryID { get; set; }
    }
}
