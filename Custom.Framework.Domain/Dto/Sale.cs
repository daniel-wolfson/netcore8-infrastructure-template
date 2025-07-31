using Custom.Domain.Optima.Models;
using Custom.Domain.Optima.Models.Enums;

namespace Custom.Domain.Optima.Dto
{
    public class Sale
    {
        /// <summary> SaleText </summary>
        public string SaleText { get; set; }
        /// <summary> SaleNopId => PackageId </summary>
        public int SaleNopId { get; set; }
        public ESaleSource SaleSource { get; set; }
        /// <summary> Current pms type search result, includes:  Opera, Optima </summary>
        public EPms Pms { get; set; }
        public string? SaleTermsHeader { get; set; }
        public string? SaleTermsBody { get; set; }

        public List<BoardBase> BoardBases { get; set; } = [];
        public string SaleFilterCategoriesIds => SaleFilterCategories?.Any() == true
            ? string.Join(",", SaleFilterCategories.Select(x => x.FilterCategoryId).ToList())
            : string.Empty;

        public List<DiscountFilter> SaleFilterCategories { get; set; }
        public bool FilterCategoryIsShow { get; set; }
        public int DataFilterDiscountId { get; set; }
        public string? DataFilterDiscountName { get; set; }
        public bool FilterCategoryIsDisabled { get; set; }
        public bool FilterCategoryIsOverrideSiteDiscount { get; set; }
        public int FilterCategoryPriority { get; set; }
        
        public int DiscountCategoryId { get; set; }
        public string? DiscountDescription { get; set; }

        public bool IsPackage { get; set; }
        public bool IsBasePrice { get; set; }
        public bool IsSpecialPopulation { get; set; }
        public bool IsSunClubDiscountOverride { get; set; }
        public bool IsWebsiteDiscountOverride { get; set; }
        public bool IsValid => BoardBases?.Count > 0;
        public bool IsNonRefundable { get; set; }
        public object IncludedFlightDetails { get; set; }
        public bool IsAssignedToFlights { get; set; }
        public bool IsNetPrice { get; set; }
        public bool ApplyOnlyForScMembers { get; set; }
        public bool IgnoreDiscountAmount { get; set; }
        public bool IgnoreDynamicFlights { get; set; }
        public bool MobileOnly { get; set; }
        public bool IsSunClubOnly { get; set; }

        public decimal AdditionalPackageCharge { get; set; }
        public decimal? CommissionRate { get; set; }
        public string? MarketingTitle { get; set; }
        public string SaleRatePlanCode { get; set; }
        public List<string> SalePackageCodes { get; set; }
        public string? SourceProfile { get; set; }
        public string? PackageCode { get; set; } = string.Empty;

        public int Priority { get; set; }
    }
}