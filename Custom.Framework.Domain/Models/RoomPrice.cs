using Custom.Domain.Optima.Models.Enums;

namespace Custom.Domain.Optima.Models
{
    public class RoomPrice
    {
        public RoomPrice()
        {
            OperaPrice = new();
        }

        public RoomPrice(decimal operaPrice)
        {
            OperaPrice = new(operaPrice);
        }

        public RoomPrice(string currency, decimal operaPrice)
        {
            OperaPrice = new(currency, operaPrice);
        }

        public int CustomerId { get; set; }

        public string? BaseCurrency { get; set; }

        public EChannel Channel { get; set; }

        public string? HotelCode { get; set; }

        public EPms Pms { get; set; } = EPms.Optima;
        public bool IsVatApply { get; set; }

        /// <summary>
        /// Title OWS Price. Always exclude VAT
        /// </summary>
        public decimal IlsOperaPrice => OperaPrice?.Get("ILS") ?? 0;

        /// <summary>
        /// without VAT
        /// </summary>
        public Price OperaPrice { get; set; }

        /// <summary>
        /// For reservation pricing comments in discounts with package codes
        /// </summary>
        public decimal OperaPriceWithoutPackages { get; set; }

        public decimal IlsDisplayOperaPrice => Math.Ceiling(IlsOperaPrice);

        /// <summary>
        /// The room final Price before Math.Floor.
        /// </summary>
        public Price? CalculatePrice { get; set; }

        public Price DisplayOperaPrice
        {
            get
            {
                var price = new Price();

                if (OperaPrice == null || OperaPrice.Prices == null)
                    return price;

                foreach (var item in OperaPrice.Prices)
                {
                    if (item.Key == "ILS")
                        price.AddOrUpdate(item.Key, IlsDisplayOperaPrice);
                    else
                        price.AddOrUpdate(item.Key, CurrencyDisplayPrice(item.Value));
                }
                price.InitDefaultPrice();
                return price;
            }
        }

        /// <summary>
        /// The room final Price after Math.Floor.
        /// </summary>
        public Price? DisplayPrice
        {
            get
            {
                if (CalculatePrice == null)
                    return null;

                var price = new Price();
                foreach (var item in CalculatePrice.Prices)
                {
                    price.AddOrUpdate(item.Key, Math.Floor(item.Value));
                }
                price.InitDefaultPrice();
                return price;
            }
        }

        public decimal IlsDisplayPrice => DisplayPrice?.Get("ILS") ?? 0;

        public decimal TotalDiscount { get; set; }

        private decimal CurrencyDisplayPrice(decimal value) => Math.Ceiling(value);
    }
}