using Custom.Framework.Configuration;
using Custom.Framework.Contracts;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Custom.Framework.Core
{
    public class ApiHttpClientFactory : IApiHttpClientFactory
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITypedHttpClientFactory<ApiHttpClient> _typedClientFactory;
        private readonly IOptions<ApiSettings> _options;

        public ApiHttpClientFactory(IHttpClientFactory httpClient,
            ITypedHttpClientFactory<ApiHttpClient> typedClientFactory, 
            IOptions<ApiSettings> options)
        {
            _httpClientFactory = httpClient;
            _typedClientFactory = typedClientFactory;
            _options = options;
        }

        /// <summary> 
        /// Creates a typed client given an associated HttpClient. 
        /// where for parameter clientName maybe use ApiHttpClientNames
        /// </summary>
        public ApiHttpClient CreateClient(string clientName)
        {
            var namedClient = _httpClientFactory.CreateClient(clientName);
            var httpClient = _typedClientFactory.CreateClient(namedClient);
            httpClient.ClientName = clientName;
            return httpClient;
        }

        /// <summary> Creates a typed client given an associated System.Net.Http.HttpClient. </summary>
        public ApiHttpClient CreateClient(HttpClient httpClient)
        {
            return _typedClientFactory.CreateClient(httpClient);
        }
    }
}
