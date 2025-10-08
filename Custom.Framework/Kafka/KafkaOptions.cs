using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Common settings used by both Kafka producers and consumers.
    /// Only truly shared configuration belongs here.
    /// </summary>
    public class KafkaOptions
    {
        /// <summary>
        /// Test timeouts configuration for Kafka operations.
        /// </summary>
        public KafkaTimeouts? Timeouts { get; set; }

        /// <summary>
        /// Common Kafka configuration shared between producers and consumers.
        /// </summary>
        public required KafkaCommonSettings Common { get; set; }

        /// <summary>
        /// List of producer configurations.
        /// </summary>
        public List<ProducerSettings> Producers { get; set; } = [];

        /// <summary>
        /// List of consumer configurations.
        /// </summary>
        public List<ConsumerSettings> Consumers { get; set; } = [];
    }

    /// <summary>
    /// Common Kafka configuration shared between producers and consumers.
    /// </summary>
    public class KafkaCommonSettings : ClientConfig
    {
        /// <summary>
        /// Service short name template.
        /// </summary>
        public string ServiceShortName { get; set; } = "{AssemblyName}";

        /// <summary>
        /// Enable idempotence for producers.
        /// </summary>
        public bool EnableIdempotence { get; set; } = false;

        /// <summary>
        /// Group ID template for consumers.
        /// </summary>
        public string GroupId { get; set; } = "{ServiceShortName}-{Name}-group";
    }

    /// <summary>
    /// Timeout settings for Kafka test operations.
    /// </summary>
    public class KafkaTimeouts
    {
        /// <summary>
        /// Time to wait for consumer initialization.
        /// </summary>
        public TimeSpan ConsumerInitialization { get; set; } = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Time to wait for message delivery.
        /// </summary>
        public TimeSpan MessageDelivery { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Time to wait for batch delivery.
        /// </summary>
        public TimeSpan BatchDelivery { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Time to wait for producer flush.
        /// </summary>
        public TimeSpan ProducerFlush { get; set; } = TimeSpan.FromSeconds(5);
    }
}