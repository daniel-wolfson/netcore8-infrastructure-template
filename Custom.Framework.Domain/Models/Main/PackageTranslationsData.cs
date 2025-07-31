using Custom.Domain.Optima.Models.Base;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Domain.Optima.Models.Main
{
    public class PackageTranslationsData : OptimaData
    {
        public int HotelID { get; set; }
        public int LanguageID { get; set; }
        public int PackageID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string? ShortDescription { get; set; }
        public string? Pic1URL { get; set; }
        public string? Pic2URL { get; set; }
    }
}
