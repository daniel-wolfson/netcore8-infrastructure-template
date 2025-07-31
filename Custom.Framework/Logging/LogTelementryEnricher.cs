using Microsoft.Extensions.Configuration;
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

public class LogTelementryEnricher : ILogEventEnricher
{
    private IConfiguration _configuration;

    public LogTelementryEnricher(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var currentActivity = Activity.Current;
        if (currentActivity == null)
            return;

        var id = currentActivity.TraceId;
        var collectorUrl = _configuration["OpenTelemetryConfig:TracingCollectorUrl"];
        if (!string.IsNullOrEmpty(collectorUrl))
        {
            var link = $"{collectorUrl}/trace/{id}";
            var property = propertyFactory.CreateProperty("TraceLink", link);
            logEvent.AddPropertyIfAbsent(property);
        }

        var machineName = Environment.MachineName; // You can add any custom property
        var machineNameProperty = propertyFactory.CreateProperty("MachineName", machineName);
        logEvent.AddPropertyIfAbsent(machineNameProperty);
    }
}


