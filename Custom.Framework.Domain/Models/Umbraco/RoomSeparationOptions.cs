namespace Custom.Domain.Optima.Models.Umbraco
{
    public class RoomSeparationOptions
    {
        public int UmbracoSearchSettingsId { get; set; } = 1;

        public string HotelCode { get; set; }
        public List<RoomCombination> Combinations { get; set; }
    }
}