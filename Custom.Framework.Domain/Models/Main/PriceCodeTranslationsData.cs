using Custom.Domain.Optima.Models.Base;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Domain.Optima.Models.Main
{
    public class PriceCodeTranslationsData : OptimaData
    {
        public int HotelID { get; set; }
        public int LanguageID { get; set; }
        public string PriceCode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Picture1Url { get; set; }
        public string Picture2Url { get; set; }
    }
}