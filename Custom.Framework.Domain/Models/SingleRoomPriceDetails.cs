using Custom.Domain.Optima.Models.Enums;

namespace Custom.Domain.Optima.Models
{
    public class SingleRoomPriceDetails
    {
        public string UniqueId { get; set; }
        public string BoardBaseCode { get; set; }
        public string OccupancyCode { get; set; }
        public string RoomCode { get; set; }
        public int SaleId { get; set; }
        public ESaleSource SaleSource { get; set; }
        public RoomPrice? GuestPrice { get; set; }
        public RoomPrice? SunClubPrice { get; set; }
    }
}