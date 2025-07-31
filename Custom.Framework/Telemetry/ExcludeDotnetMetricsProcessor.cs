using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Custom.Framework.Telemetry
{
    public class ExcludeDotnetMetricsProcessor : ITelemetryProcessor
    {
        private readonly ITelemetryProcessor _next;

        public ExcludeDotnetMetricsProcessor(ITelemetryProcessor next) => _next = next;

        public void Process(ITelemetry item)
        {
            if (item is MetricTelemetry metric && metric.Name.Contains("process.runtime.dotnet"))
            {
                return; // Exclude this metric
            }

            _next.Process(item);
        }
    }
}
