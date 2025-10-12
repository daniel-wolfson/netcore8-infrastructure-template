using System.Text.Json.Serialization;

namespace Custom.Framework.Monitoring.Grafana.Models;

/// <summary>
/// Grafana annotation model
/// Used to mark events (deployments, incidents, releases) on graphs
/// </summary>
public class GrafanaAnnotation
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("dashboardId")]
    public int? DashboardId { get; set; }

    [JsonPropertyName("dashboardUID")]
    public string? DashboardUid { get; set; }

    [JsonPropertyName("panelId")]
    public int? PanelId { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("timeEnd")]
    public long? TimeEnd { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Helper property to set time from DateTime
    /// </summary>
    [JsonIgnore]
    public DateTime DateTime
    {
        get => DateTimeOffset.FromUnixTimeMilliseconds(Time).DateTime;
        set => Time = new DateTimeOffset(value).ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Helper property to set time end from DateTime
    /// </summary>
    [JsonIgnore]
    public DateTime? DateTimeEnd
    {
        get => TimeEnd.HasValue 
            ? DateTimeOffset.FromUnixTimeMilliseconds(TimeEnd.Value).DateTime 
            : null;
        set => TimeEnd = value.HasValue 
            ? new DateTimeOffset(value.Value).ToUnixTimeMilliseconds() 
            : null;
    }
}

/// <summary>
/// Annotation creation request
/// </summary>
public class GrafanaAnnotationRequest
{
    [JsonPropertyName("dashboardUID")]
    public string? DashboardUid { get; set; }

    [JsonPropertyName("panelId")]
    public int? PanelId { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("timeEnd")]
    public long? TimeEnd { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    public static GrafanaAnnotationRequest FromAnnotation(GrafanaAnnotation annotation)
    {
        return new GrafanaAnnotationRequest
        {
            DashboardUid = annotation.DashboardUid,
            PanelId = annotation.PanelId,
            Time = annotation.Time,
            TimeEnd = annotation.TimeEnd,
            Tags = annotation.Tags,
            Text = annotation.Text
        };
    }
}

/// <summary>
/// Annotation response
/// </summary>
public class GrafanaAnnotationResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
