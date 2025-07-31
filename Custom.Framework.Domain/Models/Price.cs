using Custom.Domain.Optima.Models.Enums;

namespace Custom.Domain.Optima.Models
{
    public class Price
    {
        public Dictionary<string, decimal> Prices { get; set; } = [];

        #region ctor
        public Price() { }

        public Price(decimal ilsPrice)
        {
            Prices = new Dictionary<string, decimal> { { "ILS", ilsPrice } };
            InitDefaultPrice();
        }
        public Price(decimal ilsPrice, decimal usdPrice)
        {
            Prices = new Dictionary<string, decimal> { { "ILS", ilsPrice }, { "USD", usdPrice } };
            InitDefaultPrice();
        }
        public Price(string currency, decimal price)
        {
            Prices = new Dictionary<string, decimal> { { currency, price } };
            InitDefaultPrice();
        }
        #endregion ctor

        #region public methods

        public decimal DefaultPrice { get; set; }

        public decimal Get(string currency)
        {
            if (Prices.ContainsKey(currency))
                return Prices[currency];
            return 0;
        }

        public decimal Get(ECurrencyCode currency)
        {
            if (Prices.ContainsKey(currency.ToString()))
                return Prices[currency.ToString()];
            return 0;
        }

        public void AddOrUpdate(string currency, decimal price)
        {
            if (Prices.ContainsKey(currency))
                Prices[currency] = price;
            else
                Prices.Add(currency, price);
        }

        public void InitDefaultPrice()
        {
            if (Prices.ContainsKey("ILS"))
                DefaultPrice = Prices["ILS"];
            else
                if (Prices.ContainsKey("USD"))
                DefaultPrice = Prices["USD"];
        }

        #endregion public methods
    }
}