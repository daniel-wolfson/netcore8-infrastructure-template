using Custom.Framework.GraphQL.Models;
using Custom.Framework.GraphQL.Services;

namespace Custom.Framework.GraphQL.Queries
{
    /// <summary>
    /// Base query class for GraphQL
    /// </summary>
    public class Query
    {
        /// <summary>
        /// Simple hello query for testing
        /// </summary>
        public string Hello => "Hello, GraphQL!";

        /// <summary>
        /// Get server time
        /// </summary>
        public DateTime ServerTime() => DateTime.UtcNow;

        public async Task<List<StockQuote>> GetStockQuotes([Service] FinnhubService finnhubService)
        {
            return await finnhubService.GetMultipleStockQuotesAsync(new List<string> { "AAPL", "MSFT", "AMZN", "NVDA", "BTC-USD" });
        }
    }
}
