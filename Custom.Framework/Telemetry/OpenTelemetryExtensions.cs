using System.Diagnostics;

namespace Custom.Framework.Telemetry
{

    public static class OpenTelemetryExtensions
    {
        public static Activity? StartActivityWithTags(this ActivitySource source, string name,
            List<KeyValuePair<string, object?>> tags)
        {
            return source.StartActivity(name,
                ActivityKind.Internal,
                Activity.Current?.Context ?? new ActivityContext(), tags);
        }
    }
}