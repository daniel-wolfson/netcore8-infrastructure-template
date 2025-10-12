using System.Text.Json.Serialization;

namespace Custom.Framework.Monitoring.Grafana.Models;

/// <summary>
/// Grafana data source model
/// </summary>
public class GrafanaDataSource
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    [JsonPropertyName("orgId")]
    public int OrgId { get; set; } = 1;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "prometheus";

    [JsonPropertyName("access")]
    public string Access { get; set; } = "proxy";

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("user")]
    public string? User { get; set; }

    [JsonPropertyName("database")]
    public string? Database { get; set; }

    [JsonPropertyName("basicAuth")]
    public bool BasicAuth { get; set; }

    [JsonPropertyName("basicAuthUser")]
    public string? BasicAuthUser { get; set; }

    [JsonPropertyName("basicAuthPassword")]
    public string? BasicAuthPassword { get; set; }

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("jsonData")]
    public GrafanaDataSourceJsonData JsonData { get; set; } = new();

    [JsonPropertyName("secureJsonFields")]
    public Dictionary<string, bool> SecureJsonFields { get; set; } = new();

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("readOnly")]
    public bool ReadOnly { get; set; }
}

/// <summary>
/// Data source JSON data configuration
/// </summary>
public class GrafanaDataSourceJsonData
{
    [JsonPropertyName("httpMethod")]
    public string HttpMethod { get; set; } = "POST";

    [JsonPropertyName("timeInterval")]
    public string? TimeInterval { get; set; }

    [JsonPropertyName("queryTimeout")]
    public string? QueryTimeout { get; set; }
}

/// <summary>
/// Data source creation response
/// </summary>
public class GrafanaDataSourceResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("uid")]
    public string Uid { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
