using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Custom.Framework.Aws.AmazonSQS;

/// <summary>
/// Implementation of SQS client for high-load scenarios
/// </summary>
public class SqsClient : ISqsClient, IDisposable
{
    private readonly IAmazonSQS _sqsClient;
    private readonly AmazonSqsOptions _options;
    private readonly ILogger<SqsClient> _logger;
    private readonly ConcurrentDictionary<string, string> _queueUrlCache = new();
    private readonly SemaphoreSlim _semaphore;

    public SqsClient(
        IAmazonSQS sqsClient,
        IOptions<AmazonSqsOptions> options,
        ILogger<SqsClient> logger)
    {
        _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _semaphore = new SemaphoreSlim(_options.MaxBatchSize);
    }

    public async Task<SendMessageResponse> SendMessageAsync<T>(
        string queueName,
        T message,
        int? delaySeconds = null,
        Dictionary<string, MessageAttributeValue>? messageAttributes = null,
        CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetQueueUrlAsync(queueName, cancellationToken);
        var messageBody = SerializeMessage(message);

        var request = new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = messageBody,
            DelaySeconds = delaySeconds ?? 0
        };

        if (messageAttributes != null)
        {
            request.MessageAttributes = messageAttributes;
        }

        // Add FIFO-specific attributes if enabled
        if (_options.EnableFifoQueues && queueName.EndsWith(".fifo"))
        {
            request.MessageGroupId = typeof(T).Name;
            if (_options.EnableContentBasedDeduplication)
            {
                request.MessageDeduplicationId = GenerateDeduplicationId(messageBody);
            }
        }

        try
        {
            var response = await _sqsClient.SendMessageAsync(request, cancellationToken);
            _logger.LogDebug("Sent message {MessageId} to queue {QueueName}", response.MessageId, queueName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<SendMessageBatchResponse> SendMessageBatchAsync<T>(
        string queueName,
        IEnumerable<T> messages,
        CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetQueueUrlAsync(queueName, cancellationToken);
        var messageList = messages.ToList();

        if (messageList.Count == 0)
        {
            return new SendMessageBatchResponse();
        }

        if (messageList.Count > _options.MaxBatchSize)
        {
            _logger.LogWarning("Batch size {Count} exceeds maximum {Max}, splitting into multiple batches",
                messageList.Count, _options.MaxBatchSize);
        }

        var batches = messageList.Chunk(_options.MaxBatchSize);
        var allSuccessful = new List<SendMessageBatchResultEntry>();
        var allFailed = new List<BatchResultErrorEntry>();

        foreach (var batch in batches)
        {
            var entries = batch.Select((msg, index) =>
            {
                var entry = new SendMessageBatchRequestEntry
                {
                    Id = index.ToString(),
                    MessageBody = SerializeMessage(msg),
                    DelaySeconds = 0
                };

                // Add FIFO-specific attributes if enabled
                if (_options.EnableFifoQueues && queueName.EndsWith(".fifo"))
                {
                    entry.MessageGroupId = typeof(T).Name;
                    if (_options.EnableContentBasedDeduplication)
                    {
                        entry.MessageDeduplicationId = GenerateDeduplicationId(entry.MessageBody);
                    }
                }

                return entry;
            }).ToList();

            var request = new SendMessageBatchRequest
            {
                QueueUrl = queueUrl,
                Entries = entries
            };

            try
            {
                var response = await _sqsClient.SendMessageBatchAsync(request, cancellationToken);
                allSuccessful.AddRange(response.Successful);
                allFailed.AddRange(response.Failed);

                _logger.LogDebug("Sent batch of {Count} messages to queue {QueueName}, {Successful} successful, {Failed} failed",
                    entries.Count, queueName, response.Successful.Count, response.Failed.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message batch to queue {QueueName}", queueName);
                throw;
            }
        }

        return new SendMessageBatchResponse
        {
            Successful = allSuccessful,
            Failed = allFailed
        };
    }

    public async Task<List<Message>> ReceiveMessagesAsync(
        string queueName,
        int? maxNumberOfMessages = null,
        int? waitTimeSeconds = null,
        CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetQueueUrlAsync(queueName, cancellationToken);

        var request = new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = maxNumberOfMessages ?? _options.MaxNumberOfMessages,
            WaitTimeSeconds = waitTimeSeconds ?? _options.WaitTimeSeconds,
            VisibilityTimeout = _options.VisibilityTimeoutSeconds,
            AttributeNames = new List<string> { "All" },
            MessageAttributeNames = new List<string> { "All" }
        };

        try
        {
            var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken);
            _logger.LogDebug("Received {Count} messages from queue {QueueName}", response.Messages.Count, queueName);
            return response.Messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to receive messages from queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<List<SqsMessage<T>>> ReceiveMessagesAsync<T>(
        string queueName,
        int? maxNumberOfMessages = null,
        int? waitTimeSeconds = null,
        CancellationToken cancellationToken = default)
    {
        var messages = await ReceiveMessagesAsync(queueName, maxNumberOfMessages, waitTimeSeconds, cancellationToken);

        return messages.Select(msg =>
        {
            var sqsMessage = new SqsMessage<T>
            {
                MessageId = msg.MessageId,
                ReceiptHandle = msg.ReceiptHandle,
                MD5OfBody = msg.MD5OfBody,
                RawBody = msg.Body,
                MessageGroupId = msg.Attributes.GetValueOrDefault("MessageGroupId"),
                MessageDeduplicationId = msg.Attributes.GetValueOrDefault("MessageDeduplicationId"),
                SequenceNumber = msg.Attributes.GetValueOrDefault("SequenceNumber"),
                SenderId = msg.Attributes.GetValueOrDefault("SenderId")
            };

            // Parse attributes
            if (msg.Attributes.TryGetValue("ApproximateReceiveCount", out var receiveCount))
            {
                sqsMessage.ApproximateReceiveCount = int.Parse(receiveCount);
            }

            if (msg.Attributes.TryGetValue("SentTimestamp", out var sentTimestamp))
            {
                sqsMessage.SentTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(sentTimestamp)).DateTime;
            }

            if (msg.Attributes.TryGetValue("ApproximateFirstReceiveTimestamp", out var firstReceiveTimestamp))
            {
                sqsMessage.ApproximateFirstReceiveTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(firstReceiveTimestamp)).DateTime;
            }

            // Deserialize body
            try
            {
                sqsMessage.Body = DeserializeMessage<T>(msg.Body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize message {MessageId}", msg.MessageId);
                sqsMessage.Body = default;
            }

            return sqsMessage;
        }).ToList();
    }

    public async Task<DeleteMessageResponse> DeleteMessageAsync(
        string queueName,
        string receiptHandle,
        CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetQueueUrlAsync(queueName, cancellationToken);

        var request = new DeleteMessageRequest
        {
            QueueUrl = queueUrl,
            ReceiptHandle = receiptHandle
        };

        try
        {
            var response = await _sqsClient.DeleteMessageAsync(request, cancellationToken);
            _logger.LogDebug("Deleted message from queue {QueueName}", queueName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete message from queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<DeleteMessageBatchResponse> DeleteMessageBatchAsync(
        string queueName,
        IEnumerable<string> receiptHandles,
        CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetQueueUrlAsync(queueName, cancellationToken);
        var handleList = receiptHandles.ToList();

        if (handleList.Count == 0)
        {
            return new DeleteMessageBatchResponse();
        }

        var batches = handleList.Chunk(_options.MaxBatchSize);
        var allSuccessful = new List<DeleteMessageBatchResultEntry>();
        var allFailed = new List<BatchResultErrorEntry>();

        foreach (var batch in batches)
        {
            var entries = batch.Select((handle, index) => new DeleteMessageBatchRequestEntry
            {
                Id = index.ToString(),
                ReceiptHandle = handle
            }).ToList();

            var request = new DeleteMessageBatchRequest
            {
                QueueUrl = queueUrl,
                Entries = entries
            };

            try
            {
                var response = await _sqsClient.DeleteMessageBatchAsync(request, cancellationToken);
                allSuccessful.AddRange(response.Successful);
                allFailed.AddRange(response.Failed);

                _logger.LogDebug("Deleted batch of {Count} messages from queue {QueueName}", entries.Count, queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete message batch from queue {QueueName}", queueName);
                throw;
            }
        }

        return new DeleteMessageBatchResponse
        {
            Successful = allSuccessful,
            Failed = allFailed
        };
    }

    public async Task<ChangeMessageVisibilityResponse> ChangeMessageVisibilityAsync(
        string queueName,
        string receiptHandle,
        int visibilityTimeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetQueueUrlAsync(queueName, cancellationToken);

        var request = new ChangeMessageVisibilityRequest
        {
            QueueUrl = queueUrl,
            ReceiptHandle = receiptHandle,
            VisibilityTimeout = visibilityTimeoutSeconds
        };

        try
        {
            return await _sqsClient.ChangeMessageVisibilityAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change message visibility for queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<string> GetQueueUrlAsync(string queueName, CancellationToken cancellationToken = default)
    {
        if (_queueUrlCache.TryGetValue(queueName, out var cachedUrl))
        {
            return cachedUrl;
        }

        try
        {
            var request = new GetQueueUrlRequest { QueueName = queueName };
            var response = await _sqsClient.GetQueueUrlAsync(request, cancellationToken);
            _queueUrlCache.TryAdd(queueName, response.QueueUrl);
            return response.QueueUrl;
        }
        catch (QueueDoesNotExistException)
        {
            _logger.LogWarning("Queue {QueueName} does not exist", queueName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get queue URL for {QueueName}", queueName);
            throw;
        }
    }

    public async Task<string> CreateQueueAsync(
        string queueName,
        bool isFifo = false,
        Dictionary<string, string>? attributes = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateQueueRequest
        {
            QueueName = queueName,
            Attributes = attributes ?? new Dictionary<string, string>()
        };

        if (isFifo)
        {
            if (!queueName.EndsWith(".fifo"))
            {
                throw new ArgumentException("FIFO queue names must end with .fifo");
            }

            request.Attributes["FifoQueue"] = "true";
            if (_options.EnableContentBasedDeduplication)
            {
                request.Attributes["ContentBasedDeduplication"] = "true";
            }
        }

        // Configure dead letter queue if enabled
        if (_options.EnableDeadLetterQueue)
        {
            var dlqName = queueName + _options.DeadLetterQueueSuffix;
            try
            {
                var dlqUrl = await GetQueueUrlAsync(dlqName, cancellationToken);
                var dlqArn = await GetQueueArnAsync(dlqUrl, cancellationToken);

                var redrivePolicy = JsonSerializer.Serialize(new
                {
                    deadLetterTargetArn = dlqArn,
                    maxReceiveCount = _options.MaxReceiveCount
                });

                request.Attributes["RedrivePolicy"] = redrivePolicy;
            }
            catch (QueueDoesNotExistException)
            {
                _logger.LogWarning("Dead letter queue {DlqName} does not exist, skipping DLQ configuration", dlqName);
            }
        }

        try
        {
            var response = await _sqsClient.CreateQueueAsync(request, cancellationToken);
            _queueUrlCache.TryAdd(queueName, response.QueueUrl);
            _logger.LogInformation("Created queue {QueueName} with URL {QueueUrl}", queueName, response.QueueUrl);
            return response.QueueUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task DeleteQueueAsync(string queueName, CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetQueueUrlAsync(queueName, cancellationToken);

        try
        {
            await _sqsClient.DeleteQueueAsync(queueUrl, cancellationToken);
            _queueUrlCache.TryRemove(queueName, out _);
            _logger.LogInformation("Deleted queue {QueueName}", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task PurgeQueueAsync(string queueName, CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetQueueUrlAsync(queueName, cancellationToken);

        try
        {
            await _sqsClient.PurgeQueueAsync(queueUrl, cancellationToken);
            _logger.LogInformation("Purged queue {QueueName}", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<Dictionary<string, string>> GetQueueAttributesAsync(
        string queueName,
        List<string>? attributeNames = null,
        CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetQueueUrlAsync(queueName, cancellationToken);

        var request = new GetQueueAttributesRequest
        {
            QueueUrl = queueUrl,
            AttributeNames = attributeNames ?? new List<string> { "All" }
        };

        try
        {
            var response = await _sqsClient.GetQueueAttributesAsync(request, cancellationToken);
            return response.Attributes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get queue attributes for {QueueName}", queueName);
            throw;
        }
    }

    public async Task<int> GetApproximateMessageCountAsync(string queueName, CancellationToken cancellationToken = default)
    {
        var attributes = await GetQueueAttributesAsync(
            queueName,
            new List<string> { "ApproximateNumberOfMessages" },
            cancellationToken);

        if (attributes.TryGetValue("ApproximateNumberOfMessages", out var count))
        {
            return int.Parse(count);
        }

        return 0;
    }

    public async Task<List<string>> ListQueuesAsync(string? queueNamePrefix = null, CancellationToken cancellationToken = default)
    {
        var request = new ListQueuesRequest();

        if (!string.IsNullOrEmpty(queueNamePrefix))
        {
            request.QueueNamePrefix = queueNamePrefix;
        }

        try
        {
            var response = await _sqsClient.ListQueuesAsync(request, cancellationToken);
            return response.QueueUrls;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list queues");
            throw;
        }
    }

    public async Task SendToDeadLetterQueueAsync<T>(
        string queueName,
        T message,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var dlqName = queueName + _options.DeadLetterQueueSuffix;

        var attributes = new Dictionary<string, MessageAttributeValue>
        {
            ["OriginalQueue"] = new MessageAttributeValue
            {
                DataType = "String",
                StringValue = queueName
            },
            ["ErrorMessage"] = new MessageAttributeValue
            {
                DataType = "String",
                StringValue = errorMessage ?? "Unknown error"
            },
            ["Timestamp"] = new MessageAttributeValue
            {
                DataType = "String",
                StringValue = DateTime.UtcNow.ToString("O")
            }
        };

        await SendMessageAsync(dlqName, message, messageAttributes: attributes, cancellationToken: cancellationToken);
    }

    private string SerializeMessage<T>(T message)
    {
        if (message is string str)
        {
            return str;
        }

        return JsonSerializer.Serialize(message);
    }

    private T? DeserializeMessage<T>(string messageBody)
    {
        if (typeof(T) == typeof(string))
        {
            return (T)(object)messageBody;
        }

        return JsonSerializer.Deserialize<T>(messageBody);
    }

    private string GenerateDeduplicationId(string messageBody)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(messageBody));
        return Convert.ToBase64String(hash);
    }

    private async Task<string> GetQueueArnAsync(string queueUrl, CancellationToken cancellationToken)
    {
        var request = new GetQueueAttributesRequest
        {
            QueueUrl = queueUrl,
            AttributeNames = new List<string> { "QueueArn" }
        };

        var response = await _sqsClient.GetQueueAttributesAsync(request, cancellationToken);
        return response.Attributes["QueueArn"];
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
        _sqsClient?.Dispose();
    }
}
