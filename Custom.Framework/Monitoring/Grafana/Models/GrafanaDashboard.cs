using System.Text.Json.Serialization;

namespace Custom.Framework.Monitoring.Grafana.Models;

/// <summary>
/// Grafana dashboard model
/// </summary>
public class GrafanaDashboard
{
    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = "browser";

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 16;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("refresh")]
    public string Refresh { get; set; } = "30s";

    [JsonPropertyName("panels")]
    public List<GrafanaPanel> Panels { get; set; } = new();

    [JsonPropertyName("templating")]
    public GrafanaTemplating Templating { get; set; } = new();

    [JsonPropertyName("time")]
    public GrafanaTimeRange Time { get; set; } = new();

    [JsonPropertyName("timepicker")]
    public GrafanaTimePicker TimePicker { get; set; } = new();

    [JsonPropertyName("editable")]
    public bool Editable { get; set; } = true;

    [JsonPropertyName("fiscalYearStartMonth")]
    public int FiscalYearStartMonth { get; set; } = 0;

    [JsonPropertyName("graphTooltip")]
    public int GraphTooltip { get; set; } = 0;

    [JsonPropertyName("links")]
    public List<object> Links { get; set; } = new();

    [JsonPropertyName("liveNow")]
    public bool LiveNow { get; set; } = false;
}

/// <summary>
/// Grafana dashboard panel
/// </summary>
public class GrafanaPanel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "graph";

    [JsonPropertyName("gridPos")]
    public GrafanaGridPos GridPos { get; set; } = new();

    [JsonPropertyName("targets")]
    public List<GrafanaTarget> Targets { get; set; } = new();

    [JsonPropertyName("datasource")]
    public GrafanaDataSourceRef? Datasource { get; set; }

    [JsonPropertyName("fieldConfig")]
    public GrafanaFieldConfig FieldConfig { get; set; } = new();

    [JsonPropertyName("options")]
    public object Options { get; set; } = new { };
}

/// <summary>
/// Panel grid position
/// </summary>
public class GrafanaGridPos
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("w")]
    public int W { get; set; } = 12;

    [JsonPropertyName("h")]
    public int H { get; set; } = 8;
}

/// <summary>
/// Prometheus query target
/// </summary>
public class GrafanaTarget
{
    [JsonPropertyName("expr")]
    public string Expr { get; set; } = string.Empty;

    [JsonPropertyName("legendFormat")]
    public string? LegendFormat { get; set; }

    [JsonPropertyName("refId")]
    public string RefId { get; set; } = "A";

    [JsonPropertyName("datasource")]
    public GrafanaDataSourceRef? Datasource { get; set; }
}

/// <summary>
/// Data source reference
/// </summary>
public class GrafanaDataSourceRef
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "prometheus";

    [JsonPropertyName("uid")]
    public string? Uid { get; set; }
}

/// <summary>
/// Field configuration
/// </summary>
public class GrafanaFieldConfig
{
    [JsonPropertyName("defaults")]
    public GrafanaFieldDefaults Defaults { get; set; } = new();

    [JsonPropertyName("overrides")]
    public List<object> Overrides { get; set; } = new();
}

/// <summary>
/// Field defaults
/// </summary>
public class GrafanaFieldDefaults
{
    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("color")]
    public GrafanaColorConfig Color { get; set; } = new();
}

/// <summary>
/// Color configuration
/// </summary>
public class GrafanaColorConfig
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "palette-classic";
}

/// <summary>
/// Dashboard templating/variables
/// </summary>
public class GrafanaTemplating
{
    [JsonPropertyName("list")]
    public List<object> List { get; set; } = new();
}

/// <summary>
/// Time range
/// </summary>
public class GrafanaTimeRange
{
    [JsonPropertyName("from")]
    public string From { get; set; } = "now-6h";

    [JsonPropertyName("to")]
    public string To { get; set; } = "now";
}

/// <summary>
/// Time picker configuration
/// </summary>
public class GrafanaTimePicker
{
    [JsonPropertyName("refresh_intervals")]
    public List<string> RefreshIntervals { get; set; } = new() 
    { 
        "5s", "10s", "30s", "1m", "5m", "15m", "30m", "1h", "2h", "1d" 
    };
}

/// <summary>
/// Dashboard API request wrapper
/// </summary>
public class GrafanaDashboardRequest
{
    [JsonPropertyName("dashboard")]
    public GrafanaDashboard Dashboard { get; set; } = new();

    [JsonPropertyName("folderUid")]
    public string? FolderUid { get; set; }

    [JsonPropertyName("overwrite")]
    public bool Overwrite { get; set; } = true;

    [JsonPropertyName("message")]
    public string Message { get; set; } = "Updated by Custom.Framework";
}

/// <summary>
/// Dashboard API response
/// </summary>
public class GrafanaDashboardResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("uid")]
    public string Uid { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;
}
