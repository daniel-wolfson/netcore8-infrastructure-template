namespace Custom.Domain.Optima.Models.Availability
{
    public class CurrencyDate
    {
        public string pmsHotelCode { get; set; }
        public string currencyCode { get; set; }
        public DateTime currencyDate { get; set; }
        public int currencyRate1 { get; set; }
        public int currencyRate2 { get; set; }
        public string updatedClerk { get; set; }
        public DateTime timeUpdated { get; set; }
        public int bankRate { get; set; }
        public DateTime bankDate { get; set; }
    }
}