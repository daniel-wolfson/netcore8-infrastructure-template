using Custom.Domain.Optima.Models.Base;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Domain.Optima.Models.Main
{
    public class RoomTranslateData : OptimaData
    {
        public static RoomTranslateData Default(int hotelId, int languageId, string emptyTextReplacement)
        {
            return new RoomTranslateData()
            {
                HotelID = hotelId,
                LanguageID = languageId,
                Name = emptyTextReplacement,
                Description = emptyTextReplacement
            };
        }

        public int HotelID { get; set; }
        public int LanguageID { get; set; }
        public string RoomCategory { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Picture1Url { get; set; }
        public string Picture2Url { get; set; }
    }
}