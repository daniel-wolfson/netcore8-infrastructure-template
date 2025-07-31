using Newtonsoft.Json;

namespace Custom.Framework.HealthChecks
{
    public class HealthInfo
    {
        [JsonIgnore]
        public string Key { get; set; } = string.Empty;
        public object? Value { get; set; }
        public string? Description { get; set; }
        public TimeSpan Duration { get; set; }
        public string? Status { get; set; }
        public string? Error { get; set; }
        public object? Details { get; set; }
    }
}