using Custom.Framework.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Custom.Framework.HealthChecks
{
    public class VersionHealthCheck(IOptions<ApiSettings> appSettingsOptions) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(appSettingsOptions.Value.Version))
            {

                return Task.FromResult(HealthCheckResult.Healthy(
                    description: $"current version",
                    data: new Dictionary<string, object>() { { "Code", appSettingsOptions.Value.Version } }));
            }

            return Task.FromResult(new HealthCheckResult(
                context.Registration.FailureStatus,
                "ApiSettings key 'Version' not defined or empty"));
        }
    }
}