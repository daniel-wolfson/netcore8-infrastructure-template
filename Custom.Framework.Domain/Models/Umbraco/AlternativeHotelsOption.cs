namespace Custom.Domain.Optima.Models.Umbraco
{
    public class AlternativeHotelsOption
    {
        public int UmbracoSearchSettingsId { get; set; } = 1;
        public string OriginalHotel { get; set; }
        public List<string> AlternativeHotels { get; set; }
    }
}