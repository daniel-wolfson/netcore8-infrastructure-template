using Custom.Domain.Optima.Models.Base;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Domain.Optima.Models.Main
{
    public class TranslatePackageData1 : OptimaData
    {
        public static TranslatePackageData1 Default(int hotelId, int languageId, int packageID, string emptyTextReplacement)
        {
            return new TranslatePackageData1()
            {
                HotelID = hotelId,
                LanguageID = languageId,
                PackageID = packageID,
                Name = emptyTextReplacement,
                Description = emptyTextReplacement
            };
        }

        public int HotelID { get; set; }
        public int LanguageID { get; set; }
        public int PackageID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ShortDescription { get; set; }
        public string Pic1URL { get; set; }
        public string Pic2URL { get; set; }
        public int CustomerID { get; set; }
    }
}