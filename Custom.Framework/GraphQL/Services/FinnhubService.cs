using Custom.Framework.GraphQL.Models;
using System.Text.Json;

namespace Custom.Framework.GraphQL.Services
{
    public class FinnhubService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey = "YOUR_API_KEY";  // Replace with your Finnhub API Key

        public FinnhubService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://finnhub.io/api/v1/");
        }

        public async Task<StockQuote> GetStockQuoteAsync(string symbol)
        {
            var response = await _httpClient.GetAsync($"quote?symbol={symbol}&token={_apiKey}");
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();

            // Deserialize JSON using a dictionary for flexible field mapping
            var stockData = JsonSerializer.Deserialize<Dictionary<string, decimal>>(jsonResponse);

            if (stockData == null) return null;

            return new StockQuote
            {
                Symbol = symbol,
                CurrentPrice = stockData.GetValueOrDefault("c"),
                Change = stockData.GetValueOrDefault("d"),
                PercentChange = stockData.GetValueOrDefault("dp"),
                HighPrice = stockData.GetValueOrDefault("h"),
                LowPrice = stockData.GetValueOrDefault("l"),
                OpenPrice = stockData.GetValueOrDefault("o"),
                PreviousClosePrice = stockData.GetValueOrDefault("pc")
            };
        }

        public async Task<List<StockQuote>> GetMultipleStockQuotesAsync(List<string> symbols)
        {
            var stockQuotes = new List<StockQuote>();

            foreach (var symbol in symbols)
            {
                var quote = await GetStockQuoteAsync(symbol);
                if (quote != null)
                {
                    stockQuotes.Add(quote);
                }
            }

            return stockQuotes;
        }
    }
}
