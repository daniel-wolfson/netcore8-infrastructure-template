using Custom.Framework.Kafka;

namespace Custom.Framework.Benchmarks.Benchmarks
{
    internal class CommonSettings : KafkaCommonSettings
    {
        public bool EnableMetrics { get; set; }
        public bool EnableHealthCheck { get; set; }
    }
}