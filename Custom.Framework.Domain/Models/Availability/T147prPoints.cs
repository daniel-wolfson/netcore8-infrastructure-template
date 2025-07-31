namespace Custom.Domain.Optima.Models.Availability
{
    public class T147prPoints
    {
        public string pmsHotelCode { get; set; }
        public string clubCode { get; set; }
        public string currencyCode { get; set; }
        public DateTime fromArrival { get; set; }
        public DateTime toArrival { get; set; }
        public DateTime fromTaken { get; set; }
        public DateTime toTaken { get; set; }
        public int conversionRate { get; set; }
        public bool allowConvert { get; set; }
    }
}