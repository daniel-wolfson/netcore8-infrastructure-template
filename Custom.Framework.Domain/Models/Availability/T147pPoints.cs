namespace Custom.Domain.Optima.Models.Availability
{
    public class T147pPoints
    {
        public string pmsHotelCode { get; set; }
        public string clubCode { get; set; }
        public string currencyCode { get; set; }
        public DateTime fromArrival { get; set; }
        public DateTime toArrival { get; set; }
        public DateTime fromTaken { get; set; }
        public DateTime toTaken { get; set; }
        public int conversionRate { get; set; }
        public int pointsForReservation { get; set; }
        public int pointsForReservation2 { get; set; }
        public string conversionTypeName { get; set; }
        public int priority { get; set; }
    }
}