using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace Custom.Framework.Elastic;

/// <summary>
/// Serilog enricher that adds OpenTelemetry trace context to log events
/// </summary>
public class OpenTelemetryEnricher : ILogEventEnricher
{
    /// <summary>
    /// Enriches log events with OpenTelemetry trace information (TraceId, SpanId, Baggage, Tags)
    /// </summary>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("ParentSpanId", activity.ParentSpanId.ToString()));

            // Add baggage
            foreach (var baggage in activity.Baggage)
            {
                logEvent.AddPropertyIfAbsent(
                    propertyFactory.CreateProperty($"Baggage.{baggage.Key}", baggage.Value));
            }

            // Add tags
            foreach (var tag in activity.Tags)
            {
                logEvent.AddPropertyIfAbsent(
                    propertyFactory.CreateProperty($"Tag.{tag.Key}", tag.Value ?? string.Empty));
            }
        }
    }
}
