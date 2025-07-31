using Custom.Domain.Optima.Models.Enums;

namespace Custom.Domain.Optima.Models
{
    public class BoardBase
    {
        public string BoardBaseCode { get; set; }
        public string PriceCode { get; set; }
        public int SaleNopId { get; set; }
        public ESaleSource SaleSource { get; set; }
        public RoomPrice? GuestRoomPrice { get; set; }
        public RoomPrice? SunClubRoomPrice { get; set; }
        public RatePlanType RatePlanType { get; set; }
        public string SessionID { get; set; }
        public string RoomCategory { get; set; }
        public bool IsValid { get; set; }
        public Dictionary<string, object> GenericParameters { get; set; }
    }
}