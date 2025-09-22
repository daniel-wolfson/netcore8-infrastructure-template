using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Common settings used by both Kafka producers and consumers.
    /// Only truly shared configuration belongs here.
    /// </summary>
    public class KafkaOptions: ClientConfig
    {
        /// <summary>
        /// Gets or sets the Kafka bootstrap servers list in the format "host1:port1,host2:port2".
        /// This is the initial connection point for the Kafka cluster.
        /// </summary>
        //public string BootstrapServers { get; set; } = "localhost:9092";

        /// <summary>
        /// Gets or sets an identifier for the client application. Used for logging and metrics.
        /// </summary>
        //public string ClientId { get; set; } = "isrotel-client";

        

        /// <summary>
        /// SASL username for authentication (optional).
        /// </summary>
        //public string? SaslUsername { get; set; }

        /// <summary>
        /// SASL password for authentication (optional).
        /// </summary>
        //public string? SaslPassword { get; set; }

        /// <summary>
        /// SASL mechanism (e.g. Plain, ScramSha256) (optional).
        /// </summary>
        //public string? SaslMechanism { get; set; }

        /// <summary>
        /// Security protocol (e.g. Plaintext, Ssl, SaslSsl) (optional).
        /// </summary>
        //public string? SecurityProtocol { get; set; }
    }
}