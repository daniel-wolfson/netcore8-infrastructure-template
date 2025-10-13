using Amazon.SQS.Model;

namespace Custom.Framework.Aws.AmazonSQS;

/// <summary>
/// Interface for Amazon SQS client operations supporting high-load scenarios
/// </summary>
public interface ISqsClient
{
    /// <summary>
    /// Send a single message to a queue
    /// </summary>
    Task<SendMessageResponse> SendMessageAsync<T>(
        string queueName,
        T message,
        int? delaySeconds = null,
        Dictionary<string, MessageAttributeValue>? messageAttributes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a batch of messages to a queue (up to 10 messages)
    /// </summary>
    Task<SendMessageBatchResponse> SendMessageBatchAsync<T>(
        string queueName,
        IEnumerable<T> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Receive messages from a queue
    /// </summary>
    Task<List<Message>> ReceiveMessagesAsync(
        string queueName,
        int? maxNumberOfMessages = null,
        int? waitTimeSeconds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Receive and deserialize messages from a queue
    /// </summary>
    Task<List<SqsMessage<T>>> ReceiveMessagesAsync<T>(
        string queueName,
        int? maxNumberOfMessages = null,
        int? waitTimeSeconds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a message from a queue
    /// </summary>
    Task<DeleteMessageResponse> DeleteMessageAsync(
        string queueName,
        string receiptHandle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a batch of messages from a queue (up to 10 messages)
    /// </summary>
    Task<DeleteMessageBatchResponse> DeleteMessageBatchAsync(
        string queueName,
        IEnumerable<string> receiptHandles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Change the visibility timeout of a message
    /// </summary>
    Task<ChangeMessageVisibilityResponse> ChangeMessageVisibilityAsync(
        string queueName,
        string receiptHandle,
        int visibilityTimeoutSeconds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get queue URL by name
    /// </summary>
    Task<string> GetQueueUrlAsync(string queueName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new queue
    /// </summary>
    Task<string> CreateQueueAsync(
        string queueName,
        bool isFifo = false,
        Dictionary<string, string>? attributes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a queue
    /// </summary>
    Task DeleteQueueAsync(string queueName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purge all messages from a queue
    /// </summary>
    Task PurgeQueueAsync(string queueName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get queue attributes
    /// </summary>
    Task<Dictionary<string, string>> GetQueueAttributesAsync(
        string queueName,
        List<string>? attributeNames = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get approximate number of messages in a queue
    /// </summary>
    Task<int> GetApproximateMessageCountAsync(string queueName, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all queues
    /// </summary>
    Task<List<string>> ListQueuesAsync(string? queueNamePrefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send message to dead letter queue
    /// </summary>
    Task SendToDeadLetterQueueAsync<T>(
        string queueName,
        T message,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);
}
