using Custom.Framework.Kafka;

namespace Custom.Framework.Tests.Kafka
{
    internal class CommonSettings : KafkaCommonSettings
    {
        public bool EnableMetrics { get; set; }
        public bool EnableHealthCheck { get; set; }
    }
}