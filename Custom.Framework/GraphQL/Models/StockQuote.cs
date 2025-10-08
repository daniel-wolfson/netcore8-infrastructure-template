namespace Custom.Framework.GraphQL.Models
{
    public class StockQuote
    {
        public string Symbol { get; internal set; }
        public object CurrentPrice { get; internal set; }
        public object Change { get; internal set; }
        public object PercentChange { get; internal set; }
        public decimal HighPrice { get; internal set; }
        public decimal LowPrice { get; internal set; }
        public decimal OpenPrice { get; internal set; }
        public decimal PreviousClosePrice { get; internal set; }
    }
}