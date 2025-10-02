
namespace Custom.Framework.Kafka
{
    public class FailureInfo
    {
        public string ErrorType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public DateTime FailedAt { get; set; }
    }
}