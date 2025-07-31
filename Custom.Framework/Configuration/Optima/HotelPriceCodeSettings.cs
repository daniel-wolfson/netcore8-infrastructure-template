namespace Custom.Framework.Configuration.Optima
{
    public class HotelPriceCodeSettings
    {
        public int HotelID { get; set; }
        public string HotelCode { get; set; } = string.Empty;
        public List<string> PriceCodes { get; set; } = new();
    }
}