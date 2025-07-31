namespace Custom.Framework.Configuration.Models
{
    public static class RequestHeaderKeys
    {
        public static string UpdateSettingsCache => "X-UpdateSettingsCache";
        public static string CorrelationId => "X-CorrelationId";
        public static string HttpClientName => "X-HttpClientName";
        public static string Authorization => "Authorization";
        public static string RequestUniqueStamp => "RequestUniqueStamp";
        public static string DebugMode => "IsDebugMode";
    }
}
