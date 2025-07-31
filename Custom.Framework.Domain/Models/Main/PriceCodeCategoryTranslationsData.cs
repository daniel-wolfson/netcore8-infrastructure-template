using Custom.Domain.Optima.Models.Base;
using Newtonsoft.Json;

namespace Custom.Domain.Optima.Models.Main
{
    public class PriceCodeCategoryTranslationsData : OptimaData
    {
        [JsonProperty("hotelID")]
        public int HotelID { get; set; }

        [JsonProperty("languageID")]
        public int LanguageID { get; set; }

        [JsonProperty("priceCodeCategoryID")]
        public int PriceCodeCategoryID { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("picture1Url")]
        public object Picture1Url { get; set; }

        [JsonProperty("picture2Url")]
        public object Picture2Url { get; set; }
    }
}