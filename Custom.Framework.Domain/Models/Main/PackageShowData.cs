using Custom.Domain.Optima.Models.Base;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Domain.Optima.Models.Main
{
    public class PackageShowData : OptimaData
    {
        [JsonProperty("comparisonPriceCode")]
        public string ComparisonPriceCode { get; set; }

        [JsonProperty("displayStartDate")]
        public DateTime DisplayStartDate { get; set; }

        [JsonProperty("displayEndDate")]
        public DateTime DisplayEndDate { get; set; }

        [JsonProperty("minLOS")]
        public int MinLOS { get; set; }

        [JsonProperty("maxLOS")]
        public int MaxLOS { get; set; }

        [JsonProperty("validOnSun")]
        public bool ValidOnSun { get; set; }

        [JsonProperty("validOnMon")]
        public bool ValidOnMon { get; set; }

        [JsonProperty("validOnTue")]
        public bool ValidOnTue { get; set; }

        [JsonProperty("validOnWed")]
        public bool ValidOnWed { get; set; }

        [JsonProperty("validOnThu")]
        public bool ValidOnThu { get; set; }

        [JsonProperty("validOnFri")]
        public bool ValidOnFri { get; set; }

        [JsonProperty("validOnSat")]
        public bool ValidOnSat { get; set; }

        [JsonProperty("arriveOnSun")]
        public bool ArriveOnSun { get; set; }

        [JsonProperty("arriveOnMon")]
        public bool ArriveOnMon { get; set; }

        [JsonProperty("arriveOnTue")]
        public bool ArriveOnTue { get; set; }

        [JsonProperty("arriveOnWed")]
        public bool ArriveOnWed { get; set; }

        [JsonProperty("arriveOnThu")]
        public bool ArriveOnThu { get; set; }

        [JsonProperty("arriveOnFri")]
        public bool ArriveOnFri { get; set; }

        [JsonProperty("arriveOnSat")]
        public bool ArriveOnSat { get; set; }

        [JsonProperty("displayOrder")]
        public int DisplayOrder { get; set; }

        [JsonProperty("wing")]
        public string Wing { get; set; }

        [JsonProperty("picture1Url")]
        public string Picture1Url { get; set; }

        [JsonProperty("picture2Url")]
        public string Picture2Url { get; set; }

        [JsonProperty("priceAfterAgentDiscount")]
        public decimal PriceAfterAgentDiscount { get; set; }

        [JsonProperty("priceAfterAgentDiscountNoTax")]
        public decimal PriceAfterAgentDiscountNoTax { get; set; }

        [JsonProperty("priceAfterUserDiscount")]
        public decimal PriceAfterUserDiscount { get; set; }

        [JsonProperty("priceAfterUserDiscountNoTax")]
        public decimal PriceAfterUserDiscountNoTax { get; set; }

        [JsonProperty("startTime")]
        public string StartTime { get; set; }

        [JsonProperty("endTime")]
        public string EndTime { get; set; }

        [JsonProperty("isPreferredPackage")]
        public bool IsPreferredPackage { get; set; }

        [JsonProperty("priceGroup")]
        public string PriceGroup { get; set; }

        [JsonProperty("hotelID")]
        public int HotelID { get; set; }

        [JsonProperty("packageID")]
        public int PackageID { get; set; }

        [JsonProperty("parentID")]
        public int ParentID { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("shortDescription")]
        public string ShortDescription { get; set; }

        [JsonProperty("startDate")]
        public DateTime StartDate { get; set; }

        [JsonProperty("endDate")]
        public DateTime EndDate { get; set; }

        [JsonProperty("basePrice")]
        public decimal BasePrice { get; set; }

        [JsonProperty("basePriceNoTax")]
        public decimal BasePriceNoTax { get; set; }

        [JsonProperty("totalPrice")]
        public decimal TotalPrice { get; set; }

        [JsonProperty("totalPriceNoTax")]
        public decimal TotalPriceNoTax { get; set; }

        [JsonProperty("oldPrice")]
        public decimal OldPrice { get; set; }

        [JsonProperty("oldPriceNoTax")]
        public decimal OldPriceNoTax { get; set; }

        [JsonProperty("currencyCode")]
        public string CurrencyCode { get; set; }

        [JsonProperty("roomCategory")]
        public string RoomCategory { get; set; }

        [JsonProperty("planCode")]
        public string PlanCode { get; set; }

        [JsonProperty("languageID")]
        public int LanguageID { get; set; }

        [JsonProperty("categriesList")]
        public List<CategriesList> CategriesList { get; set; }

        [JsonProperty("isDerivedPackage")]
        public bool IsDerivedPackage { get; set; }

        [JsonProperty("isToConsiderAgentsDiscount")]
        public bool IsToConsiderAgentsDiscount { get; set; }
    }

    public class CategriesList
    {
        [JsonProperty("packagecategoryID")]
        public int PackagecategoryID { get; set; }

        [JsonProperty("displayOrder")]
        public int DisplayOrder { get; set; }
    }
}