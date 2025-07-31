namespace Custom.Framework.HealthChecks
{
    public class HealthResult
    {
        public string Name { get; set; }
        public string Environment { get; set; }
        public string AppSettings { get; set; }
        public string AssemblyName { get; set; }
        public string AssemblyLocation { get; set; }
        public string Status { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, HealthInfo>? Checks { get; set; }
    }
}