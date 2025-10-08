using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Interface for Kafka consumer operations
    /// </summary>
    public interface IKafkaConsumer : IDisposable
    {
        string[] Topics { get; }
        DateTime LastAccessTime { get; set; }
        DateTime CreatedTime { get; set; }
        int AccessCount { get; set; }
        string GroupId { get; }
        string Name { get; }
        /// <summary>
        /// Starts consuming messages from configured Kafka topics.
        /// This method initializes the consumer and begins message processing.
        /// </summary>
        void Subscribe(string[]? topics, Func<ConsumeResult<string, byte[]>, CancellationToken, Task> messageHandler);
        void Subscribe(Func<ConsumeResult<string, byte[]>, CancellationToken, Task> messageHandler);

        /// <summary>
        /// Registers a handler to be invoked when a message of type TMessage is received.
        /// </summary>
        void Subscribe<TMessage>(string[]? topics, Func<TMessage?, object, CancellationToken, Task> messageHandler);
        void Subscribe<TMessage>(Func<TMessage?, object, CancellationToken, Task> messageHandler);

        /// <summary>
        /// Gracefully stops the Kafka consumer.
        /// This method ensures that any in-progress message processing is completed
        /// and resources are properly released before shutting down.
        /// </summary>
        Task UnsubscribeAsync();

        /// <summary>
        /// Flushes pending messages in the processing pipeline.
        /// Waits until all messages currently in the channel are processed or timeout occurs.
        /// </summary>
        Task FlushAsync(TimeSpan? span = null);

        /// <summary>
        /// Gets the total number of messages that exist in Kafka topics (high watermark).
        /// This counts ALL messages in the topic, regardless of whether they've been consumed or not.
        /// </summary>
        /// <returns>Total message count across all partitions in subscribed topics</returns>
        long GetTotalMessagesInTopics();
    }
}