namespace Custom.Framework.Configuration.Optima
{
    public class CurrencyConfig : ConfigData
    {
        public string CurrencyRatesUrl { get; set; } = string.Empty;
        public int CurrencyRatesTtl { get; set; }
        public string GetAllCurrencyRates { get; set; } = string.Empty;
        public string Convert { get; set; } = string.Empty;
    }
}