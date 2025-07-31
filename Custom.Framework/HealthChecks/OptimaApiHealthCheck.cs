using Custom.Framework.Configuration;
using Custom.Framework.Contracts;
using Custom.Framework.Core;
using Custom.Framework.Helpers;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Dynamic;

namespace Custom.Framework.HealthChecks
{
    public class OptimaApiHealthCheck(IApiHttpClientFactory httpClientFactory,
        IOptions<ApiSettings> appSettingsOptions, ILogger logger) : IHealthCheck
    {
        private readonly IApiHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ApiSettings _appSettings = appSettingsOptions.Value;
        private readonly ILogger _logger = logger;

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object>() {
                { "BaseAddress", $"{_appSettings.Optima.Host}{_appSettings.Optima.RootPath}"},
                { "CheckPath", _appSettings.Optima.AvailabilityPricesUrl ?? "" }
            };

            string fullPath = data["BaseAddress"]?.ToString() + data["CheckPath"]?.ToString();

            try
            {
                var httpClient = _httpClientFactory.CreateClient(ApiHttpClientNames.OptimaAvailabilityApi);
                var availabilityRequest = MakeDummyAvailabilityRequest();

                data.Add("Request", JsonConvert.SerializeObject(availabilityRequest));

                var serviceResult = await httpClient.PostAsync<JObject, object>(
                    _appSettings.Optima.AvailabilityPricesUrl!, availabilityRequest);

                if (serviceResult != null && serviceResult.Value != null)
                {
                    var jsonString = serviceResult?.Value?.ToString() ?? string.Empty;

                    try
                    {
                        dynamic? dynamicObject = JsonConvert.DeserializeObject<ExpandoObject>(jsonString);
                        data.Add("PackagesListCount",
                        ((((((dynamicObject as IDictionary<string, object>)?
                            ["data"] as IDictionary<string, object>)?
                            ["packageToBookPerPax"] as IDictionary<string, object>)?
                            ["packageToBookList"] as List<object>)?.First() as IDictionary<string, object>)?
                            ["packagesList"] as IEnumerable<object>)?.Count() ?? 0);
                    }
                    catch (Exception)
                    {
                        data.Add("PackagesListCount", "not defined");
                    }

                    return new HealthCheckResult(status: HealthStatus.Healthy,
                        description: $"{ApiHttpClientNames.OptimaAvailabilityApi} is up and running.",
                        data: data.AsReadOnly());
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

        private JObject MakeDummyAvailabilityRequest()
        {
            var startDate = DateTime.Now.AddMonths(1);
            DateTime firstDayOfMonth = new DateTime(startDate.Year, startDate.Month, 1);
            string startDateTime = firstDayOfMonth.AddDays(14).ToString("yyyy-MM-dd");
            string endDateTime = firstDayOfMonth.AddDays(16).ToString("yyyy-MM-dd");

            JObject jsonObject = new(
                new JProperty("reqHotelsList", new JArray(
                    new JObject(new JProperty("hotelID", 100))
                )),
                new JProperty("roomsList", new JArray(
                    new JObject(
                        new JProperty("FromDate", startDateTime),
                        new JProperty("nights", 2),
                        new JProperty("adults", 2),
                        new JProperty("children", 0),
                        new JProperty("infants", 0),
                        new JProperty("languageID", 1),
                        new JProperty("showErrors", true),
                        new JProperty("decimals", 2)
                    )
                )),
                new JProperty("packagesPaxesList", new JArray(
                    new JObject(
                        new JProperty("hotelID", 100),
                        new JProperty("FromDate", startDateTime),
                        new JProperty("ToDate", endDateTime),
                        new JProperty("isLocal", true),
                        new JProperty("adults", 2),
                        new JProperty("children", 0),
                        new JProperty("infants", 0),
                        new JProperty("languageID", 1),
                        new JProperty("showErrors", true),
                        new JProperty("decimals", 2),
                        new JProperty("includeDerivedPackages", true),
                        new JProperty("customerID", 1)
                    )
                )),
                new JProperty("userName", _appSettings.Optima.UserName),
                new JProperty("password", _appSettings.Optima.Password),
                new JProperty("customerID", 1)
                );
            return jsonObject;
        }
    }
}