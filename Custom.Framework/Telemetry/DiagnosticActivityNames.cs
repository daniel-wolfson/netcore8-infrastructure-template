namespace Custom.Framework.Telemetry
{
    public static class DiagnosticActivityNames 
    {
        public static string OperationId => "activity.operation_id";
        public static string OperationName => "activity.operation_name";
        public static string ControllerName => "activity.controller_name";
        public static string ActionName => "activity.action_name";
        public static string RequestUrl => "activity.request_url";
        public static string CorrelationId => "activity.correlation_id";
        public static string RequestData => "activity.RequestData";
        public static string IsDebugMode => "activity.isDebugMode";
        public static string RootId => "activity.root_id";
        public static string ParentId => "activity.parent_id";
        public static string ActivityId => "activity.id";
        public static string DurationMs => "activity.duration_ms";
    }
}