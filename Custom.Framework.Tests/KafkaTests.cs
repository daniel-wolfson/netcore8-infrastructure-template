using Confluent.Kafka;
using Custom.Framework.Kafka;
using Moq;
using Serilog;
using System.Collections.Concurrent;
using System.Text.Json;
using Timestamp = Confluent.Kafka.Timestamp;

namespace Custom.Framework.Tests
{
    /// <summary>
    /// Integration tests for Kafka producer and consumer with different delivery semantics.
    /// These tests verify the behavior of AtMostOnce, AtLeastOnce, and ExactlyOnce delivery scenarios.
    /// </summary>
    public class KafkaTests : IDisposable
    {
        /// <summary>
        /// Default timeouts for test operations
        /// </summary>
        public static class Timeouts
        {
            public static readonly TimeSpan ConsumerInitialization = TimeSpan.FromSeconds(3);
            public static readonly TimeSpan MessageDelivery = TimeSpan.FromSeconds(10);
            public static readonly TimeSpan BatchDelivery = TimeSpan.FromSeconds(15);
            public static readonly TimeSpan ExactlyOnceDelivery = TimeSpan.FromSeconds(15);
            public static readonly TimeSpan MultipleProducersDelivery = TimeSpan.FromSeconds(20);
            public static readonly TimeSpan ProducerFlush = TimeSpan.FromSeconds(5);
            public static readonly TimeSpan TopicCreation = TimeSpan.FromSeconds(30);
        }

        private readonly ILogger _logger;
        private readonly string _testTopicPrefix;
        private readonly List<IDisposable> _disposables;
        private readonly ConcurrentBag<Message<string, string>> _receivedMessages;
        private readonly ManualResetEventSlim _messageReceivedEvent;

        /// <summary>
        /// Default bootstrap servers for testing
        /// </summary>
        public const string DefaultBootstrapServers = "localhost:9092";

        public KafkaTests()
        {
            _logger = new Mock<ILogger>().Object;
            _testTopicPrefix = $"test-topic-{Guid.NewGuid():N}";
            _disposables = new List<IDisposable>();
            _receivedMessages = new ConcurrentBag<Message<string, string>>();
            _messageReceivedEvent = new ManualResetEventSlim(false);
        }

        #region AtMostOnce Delivery Tests

        [Fact]
        public async Task AtMostOnce_ProducerConsumer_ShouldDeliverMessageAtMostOnce()
        {
            // Arrange
            var topic = $"{_testTopicPrefix}-at-most-once";
            var groupId = $"{topic}-group";
            var producerOptions = CreateProducerOptions(DeliverySemantics.AtMostOnce);
            var producer = new KafkaProducer(producerOptions, _logger);
            var consumerOptions = CreateConsumerOptions(DeliverySemantics.AtMostOnce, groupId);
            var consumer = new KafkaConsumer(consumerOptions, _logger, topic);
            await KafkaTopicManager.EnsureTopicExistsAsync(DefaultBootstrapServers, topic);

            // Act
            await consumer.SubscribeAsync(result =>
            {
                _receivedMessages.Add(result.Message);
                _messageReceivedEvent.Set();
                return Task.CompletedTask;
            });

            _disposables.Add(producer);
            _disposables.Add(consumer);
            await Task.Delay(Timeouts.ConsumerInitialization); // Allow consumer to initialize

            var testMessage = new KafkaMessage
            {
                Topic = topic,
                Key = Guid.NewGuid().ToString(),
                Value = "AtMostOnce test message"
            };

            await producer.PublishAsync(testMessage);
            await producer.FlushAsync(Timeouts.ProducerFlush);
            var messageReceived = _messageReceivedEvent.Wait(Timeouts.MessageDelivery);

            // Assert
            Assert.True(messageReceived, "Message should be delivered with AtMostOnce semantics");
            Assert.Single(_receivedMessages);
            var receivedMessage = _receivedMessages.First();
            Assert.Equal(testMessage.Key, receivedMessage.Key);
            Assert.Equal(testMessage.Value, receivedMessage.Value);

            await consumer.UnsubscribeAsync();
        }

        [Fact]
        public async Task AtMostOnce_ProducerBatch_ShouldDeliverAllMessagesAtMostOnce()
        {
            // Arrange
            var topic = $"{_testTopicPrefix}-at-most-once-batch";
            var groupId = $"{topic}-group";
            await KafkaTopicManager.EnsureTopicExistsAsync(DefaultBootstrapServers, topic);

            var messageCount = 5;
            var testMessages = Enumerable.Range(1, messageCount)
                .Select(i => new KafkaMessage
                {
                    Topic = topic,
                    Key = Guid.NewGuid().ToString(),
                    Value = $"AtMostOnce batch message {i}"
                })
                .ToList();

            var receivedMessages = new ConcurrentBag<Message<string, string>>();
            var messagesReceived = new CountdownEvent(messageCount);
            var producerOptions = CreateProducerOptions(DeliverySemantics.AtMostOnce);
            var producer = new KafkaProducer(producerOptions, _logger);
            var consumerOptions = CreateConsumerOptions(DeliverySemantics.AtMostOnce, groupId);
            var consumer = new KafkaConsumer(consumerOptions, _logger, topic);

            // Act
            await consumer.SubscribeAsync(result =>
            {
                receivedMessages.Add(result.Message);
                messagesReceived.Signal();
                return Task.CompletedTask;
            });

            _disposables.Add(producer);
            _disposables.Add(consumer);
            await Task.Delay(1000);

            await producer.PublishBatchAsync(topic, testMessages);
            await producer.FlushAsync(TimeSpan.FromSeconds(5));
            var allMessagesReceived = messagesReceived.Wait(TimeSpan.FromSeconds(15));

            // Assert
            Assert.True(allMessagesReceived, "All messages should be delivered with AtMostOnce semantics");
            Assert.Equal(messageCount, receivedMessages.Count);
            foreach (var original in testMessages)
            {
                Assert.Contains(receivedMessages, rm => rm.Key == original.Key);
            }
            await consumer.UnsubscribeAsync();
        }

        #endregion

        #region AtLeastOnce Delivery Tests

        [Fact]
        public async Task AtLeastOnce_ProducerConsumer_ShouldDeliverMessageAtLeastOnce()
        {
            // Arrange
            var topic = $"{_testTopicPrefix}-at-least-once";
            var groupId = $"{topic}-group";
            await KafkaTopicManager.EnsureTopicExistsAsync(DefaultBootstrapServers, topic);

            var testMessage = new KafkaMessage
            {
                Topic = topic,
                Key = Guid.NewGuid().ToString(),
                Value = "AtLeastOnce test message",
                Timestamp = new Timestamp(DateTimeOffset.UtcNow)
            };

            var receivedMessages = new ConcurrentBag<Message<string, string>>();
            var messageReceivedEvent = new ManualResetEventSlim(false);
            var producerOptions = CreateProducerOptions(DeliverySemantics.AtLeastOnce);
            var producer = new KafkaProducer(producerOptions, _logger);
            var consumerConfig = CreateConsumerOptions(DeliverySemantics.AtLeastOnce, groupId);
            var consumer = new KafkaConsumer(consumerConfig, _logger, topic);

            // Act
            await consumer.SubscribeAsync((result) =>
            {
                receivedMessages.Add(result.Message);
                messageReceivedEvent.Set();
                return Task.CompletedTask;
            });

            _disposables.Add(producer);
            _disposables.Add(consumer);
            await Task.Delay(1000);

            await producer.PublishAsync(testMessage);
            await producer.FlushAsync(TimeSpan.FromSeconds(5));
            var messageReceived = messageReceivedEvent.Wait(TimeSpan.FromSeconds(10));

            // Assert
            Assert.True(messageReceived, "Message should be delivered with AtLeastOnce semantics");
            Assert.True(receivedMessages.Count >= 1, "At least one message should be received");
            var receivedMessage = receivedMessages.First();
            Assert.Equal(testMessage.Key, receivedMessage.Key);
            Assert.Equal(testMessage.Value, receivedMessage.Value);

            await consumer.UnsubscribeAsync();
        }

        [Fact]
        public async Task AtLeastOnce_ProducerWithDuplicateDetection_ShouldIncludeMessageId()
        {
            // Arrange
            var topic = $"{_testTopicPrefix}-at-least-once-duplicate-detection";
            var groupId = $"{topic}-group";

            // Ensure topic exists before starting test
            await KafkaTopicManager.EnsureTopicExistsAsync(DefaultBootstrapServers, topic);

            var testMessage = new KafkaMessage
            {
                Topic = topic,
                Key = Guid.NewGuid().ToString(),
                Value = "AtLeastOnce with duplicate detection",
                Timestamp = new Timestamp(DateTimeOffset.UtcNow)
            };

            var receivedHeaders = new ConcurrentBag<Headers>();
            var messageReceivedEvent = new ManualResetEventSlim(false);

            var producerOptions = CreateProducerOptions(DeliverySemantics.AtLeastOnce);
            producerOptions.EnableDuplicateDetection = true;
            var producer = new KafkaProducer(producerOptions, _logger);

            var consumerConfig = CreateConsumerOptions(DeliverySemantics.AtLeastOnce, groupId);
            var consumer = new KafkaConsumer(consumerConfig, _logger, topic);

            // Act
            await consumer.SubscribeAsync(async (result) =>
            {
                await Task.Yield();
                receivedHeaders.Add(result.Message.Headers);
                messageReceivedEvent.Set();
            });

            _disposables.Add(producer);
            _disposables.Add(consumer);

            await Task.Delay(1000);
            await producer.PublishAsync(testMessage);
            await producer.FlushAsync(TimeSpan.FromSeconds(5));
            var messageReceived = messageReceivedEvent.Wait(TimeSpan.FromSeconds(10));

            // Assert
            Assert.True(messageReceived, "Message should be delivered");
            Assert.Single(receivedHeaders);
            var headers = receivedHeaders.First();
            var messageIdHeader = headers.FirstOrDefault(h => h.Key == "message-id");
            Assert.NotNull(messageIdHeader);
            Assert.NotNull(messageIdHeader.GetValueBytes());
            await consumer.UnsubscribeAsync();
        }

        #endregion

        #region ExactlyOnce Delivery Tests

        [Fact]
        public async Task ExactlyOnce_SingleProducerConsumer_ShouldDeliverMessageExactlyOnce()
        {
            // Arrange
            var topic = $"{_testTopicPrefix}-exactly-once";
            var groupId = $"test-group-{Guid.NewGuid():N}";
            await KafkaTopicManager.EnsureTopicExistsAsync(DefaultBootstrapServers, topic);

            var receivedMessages = new ConcurrentBag<Message<string, string>>();
            var messageReceivedEvent = new ManualResetEventSlim(false);
            var producerOptions = CreateProducerOptions(DeliverySemantics.ExactlyOnce);
            var producer = new KafkaProducer(producerOptions, _logger);
            var consumerOptions = CreateConsumerOptions(DeliverySemantics.ExactlyOnce, groupId);
            var consumer = new KafkaConsumer(consumerOptions, _logger, topic);

            // Act
            await consumer.SubscribeAsync(async (result) =>
            {
                await Task.Yield();
                receivedMessages.Add(result.Message);
                messageReceivedEvent.Set();
            });

            _disposables.Add(producer);
            _disposables.Add(consumer);
            await Task.Delay(1000);

            var testMessage = new KafkaMessage
            {
                Topic = topic,
                Key = Guid.NewGuid().ToString(),
                Value = "ExactlyOnce test message",
                Timestamp = new Timestamp(DateTimeOffset.UtcNow)
            };

            await producer.PublishAsync(testMessage);
            await producer.FlushAsync(TimeSpan.FromSeconds(5));
            var messageReceived = messageReceivedEvent.Wait(TimeSpan.FromSeconds(15));

            // Assert
            Assert.True(messageReceived, "Message should be delivered with ExactlyOnce semantics");
            Assert.Single(receivedMessages);
            var receivedMessage = receivedMessages.First();
            Assert.Equal(testMessage.Key, receivedMessage.Key);
            Assert.Equal(testMessage.Value, receivedMessage.Value);

            await consumer.UnsubscribeAsync();
        }

        [Fact]
        public async Task ExactlyOnce_MultipleProducers_ShouldMaintainTransactionalIntegrity()
        {
            // Arrange
            var topic = $"{_testTopicPrefix}-exactly-once-multiple";
            var groupId = $"test-group-{Guid.NewGuid():N}";

            // Ensure topic exists before starting test
            await KafkaTopicManager.EnsureTopicExistsAsync(DefaultBootstrapServers, topic);

            var messagesPerProducer = 3;
            var producerCount = 2;
            var receivedMessages = new ConcurrentBag<Message<string, string>>();
            var expectedMessageCount = messagesPerProducer * producerCount;
            var messagesReceived = new CountdownEvent(expectedMessageCount);
            var consumerConfig = CreateConsumerOptions(DeliverySemantics.ExactlyOnce, groupId);
            var consumer = new KafkaConsumer(consumerConfig, _logger, topic);

            await consumer.SubscribeAsync(async (result) =>
            {
                try
                {
                    await Task.Yield();
                    receivedMessages.Add(result.Message);
                    messagesReceived.Signal();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Consumer handler exception: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            });

            _disposables.Add(consumer);
            await Task.Delay(1000);

            Console.WriteLine($"Consumer started. Expected messages: {expectedMessageCount}");

            // Act - Create multiple producers and send messages concurrently
            var producerTasks = Enumerable.Range(1, producerCount).Select(async producerIndex =>
            {
                var producerOptions = CreateProducerOptions(DeliverySemantics.ExactlyOnce);
                var producer = new KafkaProducer(producerOptions, _logger);
                _disposables.Add(producer);

                var messages = Enumerable.Range(1, messagesPerProducer)
                    .Select(i => new KafkaMessage
                    {
                        Topic = topic,
                        Key = $"producer-{producerIndex}-message-{i}",
                        Value = $"ExactlyOnce message {i} from producer {producerIndex}",
                        Timestamp = new Timestamp(DateTimeOffset.UtcNow)
                    })
                    .ToList();

                Console.WriteLine($"Producer {producerIndex} sending {messages.Count} messages");

                foreach (var message in messages)
                {
                    try
                    {
                        await producer.PublishAsync(message);
                        Console.WriteLine($"Producer {producerIndex} sent message: {message.Key}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Producer {producerIndex} failed to send message {message.Key}: {ex.Message}");
                    }
                }
                await producer.FlushAsync(TimeSpan.FromSeconds(5));
                Console.WriteLine($"Producer {producerIndex} flushed");

                return messages;
            });

            var tasksValues = await Task.WhenAll(producerTasks);
            var allMessages = tasksValues.SelectMany(m => m).ToList();

            Console.WriteLine($"All producers completed. Waiting for messages. Expected: {expectedMessageCount}");
            Console.WriteLine($"Countdown current count before wait: {messagesReceived.CurrentCount}");

            var allMessagesReceived = messagesReceived.Wait(TimeSpan.FromSeconds(20));

            Console.WriteLine($"Wait completed. Result: {allMessagesReceived}");
            Console.WriteLine($"Final countdown current count: {messagesReceived.CurrentCount}");
            Console.WriteLine($"Messages in bag: {receivedMessages.Count}");

            // Assert
            Assert.True(allMessagesReceived, $"All messages should be delivered with ExactlyOnce semantics. " +
                $"Final countdown: {messagesReceived.CurrentCount}");
            Assert.Equal(expectedMessageCount, receivedMessages.Count);

            foreach (var original in allMessages)
            {
                Assert.Contains(receivedMessages, rm => rm.Key == original.Key);
            }

            await consumer.UnsubscribeAsync();
        }

        #endregion

        #region Error Handling Tests

        /// <summary>
        /// this method tests that if the consumer handler throws an exception,
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        [Fact]
        public async Task Consumer_HandlerException_ShouldContinueProcessing()
        {
            // Arrange - Use multiple partitions to allow parallel processing
            var topic = $"{_testTopicPrefix}-test-handling";
            var groupId = $"test-group-{Guid.NewGuid():N}";
            var partitionCount = 3;
            var messageCount = 3;
            var receivedMessages = new ConcurrentBag<Message<string, string>>();
            var processedMessages = new ConcurrentBag<Message<string, string>>();
            var deathLetterQueueMessages = new ConcurrentBag<Message<string, string>>();
            var message2ErrorCount = 0;

            // Create topic with multiple partitions to avoid ordering issues
            await KafkaTopicManager.EnsureTopicExistsAsync(DefaultBootstrapServers, topic, partitionCount);
            await KafkaTopicManager.EnsureTopicExistsAsync(DefaultBootstrapServers, $"{topic}-dlq");

            var producerOptions = CreateProducerOptions(DeliverySemantics.AtLeastOnce);
            var producer = new KafkaProducer(producerOptions, _logger);
            var consumerOptions = CreateConsumerOptions(DeliverySemantics.AtLeastOnce, groupId);
            var consumer = new KafkaConsumer(consumerOptions, _logger, topic);

            // Act
            await consumer.SubscribeAsync((result) =>
            {
                processedMessages.Add(result.Message);

                // Simulate error on message 2, but only fail it a few times
                if (result.Offset == 1)
                {
                    var currentErrorCount = Interlocked.Increment(ref message2ErrorCount);
                    if (currentErrorCount <= 2) // Fail first 2 attempts
                    {
                        deathLetterQueueMessages.Add(result.Message);
                        throw new InvalidOperationException("TestInvalidOperationException");
                    }
                }

                receivedMessages.Add(result.Message);
                return Task.CompletedTask;
            });

            _disposables.Add(producer);
            _disposables.Add(consumer);
            await Task.Delay(Timeouts.ConsumerInitialization);

            // Send messages to different partitions using keys
            for (int i = 1; i <= messageCount; i++)
            {
                var message = new KafkaMessage
                {
                    Topic = topic,
                    Key = Guid.NewGuid().ToString(),
                    Value = $"Error handling test message {i}",
                    Timestamp = new Timestamp(DateTimeOffset.UtcNow)
                };

                // Use different keys to distribute across partitions
                var key = $"key-{i}";
                await producer.PublishAsync(message);
            }
            await producer.FlushAsync(Timeouts.ProducerFlush);

            // Wait for all messages to be eventually processed
            var timeout = DateTime.UtcNow.Add(TimeSpan.FromSeconds(20));
            while (receivedMessages.Count < messageCount && DateTime.UtcNow < timeout)
            {
                await Task.Delay(200);
            }

            // Assert
            Assert.NotEqual(messageCount, receivedMessages.Count);
            Assert.Single(deathLetterQueueMessages);

            // Verify message 2 was retried (processed more than once)
            //var message2ProcessCount = processedMessages.Count(x => x.Order != 2);
            //message2ErrorCount = deathLetterQueueMessages.Count(x => x.Order == 2);
            //Assert.True(message2ProcessCount > 1, $"Message 2 should have been retried. Actual process count: {message2ProcessCount}");
            //Assert.True(message2ErrorCount >= 2, $"Message 2 should have failed at least 2 times. Actual error count: {message2ErrorCount}");

            await consumer.UnsubscribeAsync();
        }

        [Fact]
        public async Task Consumer_HandlerException_ShouldPublishToDeadLetterQueue()
        {
            // Arrange
            var topic = $"{_testTopicPrefix}-dlq-test";
            var dlqTopic = $"{topic}-dlq";
            var groupId = $"test-group-{Guid.NewGuid():N}";
            var receivedMessages = new ConcurrentBag<Message<string, string>>();
            var dlqMessages = new ConcurrentBag<string>();
            var dlqMessagesReceived = new CountdownEvent(1); // Expecting 1 DLQ message
            await KafkaTopicManager.EnsureTopicExistsAsync(DefaultBootstrapServers, topic);
            await KafkaTopicManager.EnsureTopicExistsAsync(DefaultBootstrapServers, dlqTopic);

            // Create producer
            var producerOptions = CreateProducerOptions(DeliverySemantics.AtLeastOnce);
            var producer = new KafkaProducer(producerOptions, _logger);

            // Create main consumer that will fail on certain messages
            var consumerOptions = CreateConsumerOptions(DeliverySemantics.AtLeastOnce, groupId);
            consumerOptions.EnableDeadLetterQueue = true;
            consumerOptions.MaxRetries = 2; // Low retry count for faster test
            var consumer = new KafkaConsumer(consumerOptions, _logger, topic);

            // Create DLQ consumer to verify messages arrive in DLQ
            var dlqConsumerOptions = CreateConsumerOptions(DeliverySemantics.AtLeastOnce, $"{groupId}-dlq");
            var dlqConsumer = new KafkaConsumer(dlqConsumerOptions, _logger, dlqTopic);

            // Act
            await consumer.SubscribeAsync((result) =>
            {
                if (result.Message.Value.Contains("error"))
                {
                    throw new InvalidOperationException("Simulated processing error");
                }
                receivedMessages.Add(result.Message);
                return Task.CompletedTask;
            });

            await dlqConsumer.SubscribeAsync((result) =>
            {
                dlqMessages.Add(result.Message.Value);
                dlqMessagesReceived.Signal();
                return Task.CompletedTask;
            });

            _disposables.Add(producer);
            _disposables.Add(consumer);
            _disposables.Add(dlqConsumer);
            await Task.Delay(Timeouts.ConsumerInitialization);

            // Send success message and error message
            await producer.PublishAsync(new KafkaMessage
            {
                Topic = topic,
                Key = Guid.NewGuid().ToString(),
                Value = "success message"
            });

            await producer.PublishAsync(new KafkaMessage
            {
                Topic = topic,
                Key = Guid.NewGuid().ToString(),
                Value = "error message that will fail"
            });

            await producer.FlushAsync(Timeouts.ProducerFlush);

            // Wait for DLQ message
            var dlqReceived = dlqMessagesReceived.Wait(TimeSpan.FromSeconds(15));

            // Assert
            Assert.True(dlqReceived, "Failed message should be published to DLQ");
            Assert.Single(dlqMessages);
            Assert.Single(receivedMessages); // Only success message should be processed

            // Verify DLQ message contains original data and error info
            var dlqMessage = JsonSerializer.Deserialize<JsonElement>(dlqMessages.First());
            Assert.Equal("error message that will fail", dlqMessage.GetProperty("OriginalValue").GetString());
            Assert.Equal("InvalidOperationException", dlqMessage.GetProperty("FailureInfo").GetProperty("ErrorType").GetString());

            await consumer.UnsubscribeAsync();
            await dlqConsumer.UnsubscribeAsync();
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// Creates a producer configuration for testing
        /// </summary>
        public static ProducerOptions CreateProducerOptions(
            DeliverySemantics semantics,
            string? bootstrapServers = null,
            bool enableDuplicateDetection = false)
        {
            var config = new ProducerOptions
            {
                BootstrapServers = bootstrapServers ?? DefaultBootstrapServers,
                ClientId = $"test-producer-{Guid.NewGuid():N}",
                DeliverySemantics = semantics,
                EnableMetrics = false,
                EnableHealthCheck = false,
                MaxRetries = 3,
                LingerMs = 0, // Immediate send for tests
                BatchSize = 16384, // 16KB
                MessageMaxBytes = 1024 * 1024, // 1MB
                CompressionType = "None",
                CompressionLevel = 0,
                EnableIdempotence = semantics != DeliverySemantics.AtMostOnce,
                TransactionTimeoutMs = semantics == DeliverySemantics.ExactlyOnce ? 30000 : null,
                EnableDuplicateDetection = enableDuplicateDetection,
                RetryBackoffMs = 100
            };

            // Set RetryBackoffMs using reflection since the setter is internal
            //var retryBackoffProperty = typeof(ProducerOptions).GetProperty(nameof(ProducerOptions.RetryBackoffMs));
            //retryBackoffProperty?.SetValue(config, TimeSpan.FromMilliseconds(100));

            return config;
        }

        /// <summary>
        /// Creates a consumer configuration for testing
        /// </summary>
        public static ConsumerOptions CreateConsumerOptions(
            DeliverySemantics semantics,
            string groupId,
            string? bootstrapServers = null)
        {
            return new ConsumerOptions
            {
                BootstrapServers = bootstrapServers ?? DefaultBootstrapServers,
                ClientId = $"test-consumer-{Guid.NewGuid():N}",
                GroupId = groupId,
                DeliverySemantics = semantics,
                EnableMetrics = false,
                EnableHeBalthCheck = false,
                MaxRetries = 3,
                RetryBackoffMs = TimeSpan.FromMilliseconds(100),
                MaxFetchBytes = 1024 * 1024, // 1MB
                MaxPartitionFetchBytes = 512 * 1024, // 512KB
                AutoCommitIntervalMs = semantics == DeliverySemantics.AtMostOnce ? 1000 : null
            };
        }

        /// <summary>
        /// Generates a unique topic name for testing
        /// </summary>
        public static string GenerateTopicName(string prefix)
        {
            return $"{prefix}-{Guid.NewGuid():N}";
        }

        #endregion Helper methods

        #region Cleanup

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                try
                {
                    disposable?.Dispose();
                }
                catch (Exception ex)
                {
                    // Log cleanup errors but don't fail the test
                    Console.WriteLine($"Error disposing resource: {ex.Message}");
                }
            }
            _disposables.Clear();
        }

        #endregion
    }
}