namespace Custom.Domain.Optima.Models.Umbraco
{
    public class HotelRoomFilter
    {
        public int UmbracoSearchSettingsId { get; set; } = 1;
        public string HotelCode { get; set; }
        public int HotelId { get; set; }
        public List<RoomFilter>? Rooms { get; set; }
    }
}