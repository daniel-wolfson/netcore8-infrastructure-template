using Custom.Domain.Optima.Models.Base;

namespace Custom.Domain.Optima.Models.Main
{
    public class PriceCodeCategoryLinkData : OptimaData
    {
        public int PriceCodeCategoryID { get; set; }
        public int HotelID { get; set; }
        public string PriceCode { get; set; }
    }
}