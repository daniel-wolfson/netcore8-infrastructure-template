using Newtonsoft.Json;

namespace Custom.Domain.Optima.Models.Umbraco
{
    public class AvailableProperties
    {
        public int UmbracoSearchSettingsId { get; set; } = 1;
        public string? HotelCode { get; set; }
        public List<string> RoomCodes { get; set; } = [];
        public List<string> ConnectingDoorRooms { get; set; } = [];
    }
}