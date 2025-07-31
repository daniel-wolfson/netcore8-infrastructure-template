using Custom.Domain.Optima.Models.Base;

namespace Custom.Domain.Optima.Models.Main
{
    public class RoomPaxData : OptimaData
    {
        public decimal Serial { get; set; }
        public int HotelID { get; set; }
        public string? RoomCategory { get; set; }
        public int Adults { get; set; }
        public int Children { get; set; }
        public int Infants { get; set; }
        public decimal PmsSerial { get; set; }
    }
}