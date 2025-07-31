using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Custom.Framework.HealthChecks
{
    public class CompositeHealthCheck : IHealthCheck
    {
        private readonly IEnumerable<IHealthCheck> checks;

        public CompositeHealthCheck(IEnumerable<IHealthCheck> checks)
        {
            this.checks = checks ?? throw new ArgumentNullException(nameof(checks));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var results = new List<HealthCheckResult>();

            foreach (var check in checks)
            {
                var result = await check.CheckHealthAsync(context, cancellationToken);
                results.Add(result);
            }

            // Determine the overall status based on individual check results.
            var isHealthy = results.All(result => result.Status == HealthStatus.Healthy);

            // You can customize the logic for determining the overall status here.
            var status = isHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy;

            // Build a description that includes the status of individual checks.
            //var description = string.Join("\n", results.Select(r => $"{r.FilterName}: {r.Status} - {r.Description}"));
            var description = string.Join("\n", results.Select(r => $"{r.Status} - {r.Description}"));

            return new HealthCheckResult(status, description);
        }
    }
}