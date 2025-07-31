using Serilog.Events;

public class LatencyLogEvent(DateTimeOffset timestamp,
    LogEventLevel level, Exception? exception,
    MessageTemplate messageTemplate,
    IEnumerable<LogEventProperty> properties) 
    : LogEvent(timestamp, level, exception, messageTemplate, properties)
{
    public string AdditionalMessage { get; set; } = string.Empty;
}


