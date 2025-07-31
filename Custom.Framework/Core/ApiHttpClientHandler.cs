using Custom.Framework.Configuration.Models;
using Custom.Framework.Helpers;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace Custom.Framework.Core
{
    public class ApiHttpClientHandler : HttpClientHandler
    {
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApiHttpClientHandler(IHttpContextAccessor httpContextAccessor, ILogger logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;

            // Ignore certificate validation
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = default!;
            string correlationId = GetCorrelationId(request);

            request.Options.TryGetValue(new HttpRequestOptionsKey<string>(RequestHeaderKeys.HttpClientName), out string? clientname);
            request.Options.Set(new HttpRequestOptionsKey<string>(RequestHeaderKeys.RequestUniqueStamp), Guid.NewGuid().ToString());
            request.Options.Set(new HttpRequestOptionsKey<string>(RequestHeaderKeys.CorrelationId), correlationId);

            var sw = Stopwatch.StartNew();
            try
            {
                _logger.Information("{ClientName} httpClient sending to {RequestUri}. CorrelationId: {correlationId}",
                    clientname, request.RequestUri, correlationId);

                response = await base.SendAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                sw.Stop();
                response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(
                        $"Error occurred while sending request. " +
                        $"TimeElapsed: {sw.ElapsedMilliseconds}." +
                        $"\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}."),
                    ReasonPhrase = $"Internal Server Error",
                    RequestMessage = request
                };
            }
            finally
            {
                sw.Stop();
                if (response != null && response.IsSuccessStatusCode)
                {
                    _logger.Information("{TITLE} {CLIENTNAME} httpClient finished successfully. TimeElapsed: {TIMEELAPSED} ms. CorrelationId: {CORRELATIONID}",
                    ApiHelper.LogTitle(), clientname, sw.ElapsedMilliseconds, correlationId);
                }
            }
            return response;
        }

        private string GetCorrelationId(HttpRequestMessage request)
        {
            var existCorrelationHeader = _httpContextAccessor.HttpContext?.Request.Headers[RequestHeaderKeys.CorrelationId].ToString();
            var correlationId = !string.IsNullOrEmpty(existCorrelationHeader) ? existCorrelationHeader : Guid.NewGuid().ToString();
            request.Options.TryAdd(RequestHeaderKeys.CorrelationId, correlationId);
            return correlationId ?? Guid.NewGuid().ToString();
        }

        public static async Task<string> GetContentDataAsync(HttpContent? content)
        {
            if (content == null) return "";
            using Stream responseStream = await content.ReadAsStreamAsync();
            responseStream.Seek(0, SeekOrigin.Begin);
            using StreamReader reader = new(responseStream, Encoding.UTF8);
            string responseContent = await reader.ReadToEndAsync();
            return responseContent;
        }
    }
}
