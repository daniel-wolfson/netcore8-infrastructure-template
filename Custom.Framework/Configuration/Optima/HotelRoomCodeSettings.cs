namespace Custom.Framework.Configuration.Optima
{
    public class HotelRoomCodeSettings
    {
        public int HotelID { get; set; }
        public string HotelCode { get; set; } = string.Empty;
        public Dictionary<string, string> RoomCodes { get; set; } = new();

    }
}