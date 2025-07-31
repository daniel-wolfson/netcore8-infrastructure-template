using System;
using System.Threading.Tasks;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Interface for Kafka consumer operations
    /// </summary>
    public interface IKafkaConsumer
    {
        /// <summary>
        /// Starts consuming messages from configured Kafka topics.
        /// This method initializes the consumer and begins message processing.
        /// </summary>
        void Start();

        /// <summary>
        /// Gracefully stops the Kafka consumer.
        /// This method ensures that any in-progress message processing is completed
        /// and resources are properly released before shutting down.
        /// </summary>
        /// <returns>A Task representing the asynchronous stop operation</returns>
        Task StopAsync();
    }
}