using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Net.Mime;

namespace Custom.Framework.HealthChecks
{
    public static class ApiHealthCheckResponseWriter
    {
        public static async Task Write<T>(HttpContext context, HealthReport report) where T : class
        {
            var environmentName = context.RequestServices.GetService<IHostEnvironment>()?.EnvironmentName;
            var assemblyLocation = Path.GetDirectoryName(typeof(T).Assembly.Location) ?? "";

            var result = JsonConvert.SerializeObject(
                new HealthResult
                {
                    Name = typeof(T).Assembly.GetName().Name!,
                    AssemblyName = typeof(T).Assembly.GetName().FullName,
                    AssemblyLocation = assemblyLocation!.Replace("\\", "/"),
                    Environment = environmentName!,
                    AppSettings = $"{Path.Combine(assemblyLocation!, $"appsettings.{environmentName}.json").Replace("\\", "/")}",
                    Status = report.Status.ToString(),
                    Duration = report.TotalDuration,
                    Checks = report.Entries.Select(e => new HealthInfo
                    {
                        Key = e.Key,
                        Value = e.Value.Data.Keys.Contains("Code") ? e.Value.Data?["Code"] : null,
                        Description = e.Value.Description!,
                        Duration = e.Value.Duration,
                        Status = Enum.GetName(typeof(HealthStatus), e.Value.Status)!,
                        Error = e.Value.Exception?.Message!,
                        Details = e.Value.Data?.Count == 1 && e.Value.Data.Keys.Contains("Code")
                            ? null : e.Value.Data,
                    }).ToDictionary(k => k.Key, v => v)
                }, Formatting.None,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsync(result);
        }
    }
}