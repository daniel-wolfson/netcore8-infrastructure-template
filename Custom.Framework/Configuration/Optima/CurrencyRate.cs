namespace Custom.Framework.Configuration.Optima
{
    public class CurrencyRate
    {
        public long Id { get; set; }
        public string FromCurrency { get; set; } = null!;
        public string ToCurrency { get; set; } = null!;
        //[Precision(18, 2)]
        public decimal Rate { get; set; }
        public DateTime UpdatedOn { get; set; }
        public string? HotelCode { get; set; }
    }
}
