namespace Custom.Domain.Optima.Models
{
    public class OrderDetailsSummary
    {
        public string RoomTitle { get; set; }
        public string Occupancy { get; set; }
        public string BoardBaseName { get; set; }
        public string DiscountTitle { get; set; }
        public decimal SunClubPrice { get; set; }
        public decimal GuestPrice { get; set; }
        public int TotalRooms { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal MaxFlightPaymentAmount { get; set; }
        public decimal SunClubJoiningPrice { get; set; }
        public bool IsNopRegisterRequest { get; set; }
    }
}
