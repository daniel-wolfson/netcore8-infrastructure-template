using Custom.Domain.Optima.Models.Base;

namespace Custom.Framework.Models.Rooms
{
    public class RoomsData : OptimaData
    {
        public string RoomCategory { get; set; }
        public int HotelID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Wing { get; set; }
        public bool Active { get; set; }
        public int SortOrder { get; set; }
        public bool IsConnectingRoom { get; set; }
        public bool MinPriceRoom { get; set; }
        public bool RequestedRoom { get; set; }
        public string PmsRoomCategory { get; set; }
        public object GlobalRoomCategory { get; set; }
        public object GlobalRoomType { get; set; }
        public object Street { get; set; }
        public object Quarter { get; set; }
        public object FullAddress { get; set; }
        public object Zip { get; set; }
        public object Longtitude { get; set; }
        public object Latitude { get; set; }
    }
}
