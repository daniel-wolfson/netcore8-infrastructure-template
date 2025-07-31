using Custom.Domain.Optima.Models.Base;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Domain.Optima.Models.Main
{
    public class PriceCodeCategoryData : OptimaData
    {
        public int HotelID { get; set; }
        public int PriceCodeCategoryID { get; set; }
        public string Description { get; set; }
        public string ShortDescription { get; set; }
        public bool Delete { get; set; }
        public int DisplayOrder { get; set; }

        [NotMapped]
        public Dictionary<string, string>? GenericParameters { get; set; }
    }
}