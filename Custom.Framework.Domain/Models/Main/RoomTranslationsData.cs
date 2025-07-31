using Custom.Domain.Optima.Models.Base;

namespace Custom.Domain.Optima.Models.Main
{
    public class RoomTranslationsData : OptimaData
    {
        public int HotelID { get; set; }
        public int LanguageID { get; set; }
        public string RoomCategory { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Picture1Url { get; set; } = string.Empty;
        public string Picture2Url { get; set; } = string.Empty;
    }
}
