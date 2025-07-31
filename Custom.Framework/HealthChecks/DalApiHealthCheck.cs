using Custom.Framework.Configuration;
using Custom.Framework.Core;
using Custom.Framework.Helpers;
using Custom.Framework.Exceptions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;
using Custom.Framework.Configuration.Optima;

namespace Custom.Framework.HealthChecks
{
    public class DalApiHealthCheck<TApiSettings>(
        IHttpClientFactory httpClientFactory,
        IOptions<TApiSettings> appSettingsOptions, 
        ILogger logger) : IHealthCheck
        where TApiSettings : ApiSettings
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly TApiSettings _appSettings = appSettingsOptions.Value;
        private readonly ILogger _logger = logger;

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object>() {
                { "Host", _appSettings.Dal.Host },
                { "CheckPath", _appSettings.Currency?.CurrencyRatesUrl ?? "" }
            };
            string fullPath = data["Host"]?.ToString() + data["CheckPath"]?.ToString();

            try
            {
                using var httpClient = _httpClientFactory.CreateClient(ApiHttpClientNames.DalApi);
                var response = await httpClient.GetAsync(_appSettings.Currency?.CurrencyRatesUrl
                    ?? throw new ApiException("appSettings.Currency.CurrencyRatesUrl not defined"), cancellationToken);

                List<CurrencyRate>? currencyRates = default;
                if (response.IsSuccessStatusCode)
                {
                    string res = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(res))
                    {
                        currencyRates = JsonConvert.DeserializeObject<List<CurrencyRate>>(res);
                        data.Add("CurrencyRatesCount", currencyRates?.Count ?? 0);

                        return new HealthCheckResult(status: HealthStatus.Healthy,
                            description: $"{ApiHttpClientNames.DalApi} is up and running.",
                            data: data.AsReadOnly());
                    }
                }

                _logger.Error("{TITLE} Currency rates response content is empty.", ApiHelper.LogTitle());

                return new HealthCheckResult(status: HealthStatus.Unhealthy,
                  description: $"HealthCheck to {fullPath} failed.",
                  data: data.AsReadOnly());
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} Currency rates response exeption: {EX}", ApiHelper.LogTitle(), ex);

                data.Add("ErrorInfo", $"Exception: {ex.InnerException?.Message ?? ex.Message}");
                data.Add("StackTrace", ex.StackTrace ?? "");

                return new HealthCheckResult(status: HealthStatus.Unhealthy,
                    description: $"HealthCheck to {fullPath} failed.",
                    data: data.AsReadOnly());
            }
        }
    }
}