namespace Custom.Framework.Telemetry
{
    public class OpenTelemetrySettings
    {
        public static string ServiceName { get; }
        public static string ServiceVersion { get; }

        static OpenTelemetrySettings()
        {
            ServiceName = typeof(OpenTelemetrySettings).Assembly.GetName().Name!;
            ServiceVersion = typeof(OpenTelemetrySettings).Assembly.GetName().Version!.ToString();
        }
    }
}
