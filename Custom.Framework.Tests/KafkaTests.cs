using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Custom.Framework.Kafka;
using Custom.Framework.TestFactory.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Serilog;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;
using static Confluent.Kafka.ConfigPropertyNames;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;
using Timestamp = Confluent.Kafka.Timestamp;

namespace Custom.Framework.Tests
{
    /// <summary>
    /// Integration tests for Kafka producer and consumer with different delivery semantics.
    /// These tests verify the behavior of AtMostOnce, AtLeastOnce, and ExactlyOnce delivery scenarios.
    /// </summary>
    public class KafkaTests : IDisposable
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITestOutputHelper _output;
        private readonly List<IDisposable> _disposables;
        private int _messageCount = 3;
        private readonly KafkaOptions _settings;
        private readonly IKafkaProducer _producer;
        private readonly IKafkaConsumer _consumer;
        private readonly string _topic;

        public KafkaTests(ITestOutputHelper output)
        {
            _output = output;
            _disposables = [];
            var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

            _configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)               // Important in test projects
                .AddJsonFile("appsettings.Test.json", optional: false, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();
            services.AddKafka(_configuration);

            _logger = Log.Logger = new TestLoggerWrapper(_output);
            services.AddSingleton(Log.Logger);

            services.AddKeyedSingleton<IKafkaProducer>("Test", (serviceProvider, key) =>
            {
                var options = serviceProvider.GetRequiredService<IOptionsMonitor<ProducerSettings>>();
                var logger = serviceProvider.GetService<ILogger>()!;
                return new KafkaProducer(key.ToString()!, options, logger);
            });
            services.AddKeyedSingleton<IKafkaProducer>("DeadLetter", (serviceProvider, key) =>
            {
                var options = serviceProvider.GetRequiredService<IOptionsMonitor<ProducerSettings>>();
                var logger = serviceProvider.GetService<ILogger>()!;
                return new KafkaProducer(key.ToString()!, options, logger);
            });
            services.AddKeyedSingleton<IKafkaConsumer>("Test", (serviceProvider, key) =>
            {
                var options = serviceProvider.GetRequiredService<IOptionsMonitor<ConsumerSettings>>();
                var logger = serviceProvider.GetService<ILogger>()!;
                return new KafkaConsumer(key.ToString()!, options, logger);
            });

            services.AddLogging(builder => builder.AddSerilog());

            _serviceProvider = services.BuildServiceProvider();
            _settings = _serviceProvider
                .GetService<IOptionsMonitor<KafkaOptions>>()?.Get("Kafka")
                ?? throw new ArgumentNullException("KafkaOptions not defined");

            var topics = _settings.Consumers.SelectMany(x => x.Topics);
            foreach (var topic in topics)
            {
                ClearKafkaTopicAsync(topic).GetAwaiter().GetResult();
            }

            _producer = _serviceProvider.GetKeyedService<IKafkaProducer>("Test")!;
            _consumer = _serviceProvider.GetKeyedService<IKafkaConsumer>("Test")!;
            _topic = _producer.Topics.FirstOrDefault() ?? "Test";
            _disposables.Add(_producer);
            _disposables.Add(_consumer);
        }

        #region AtMostOnce Delivery Tests

        [Fact]
        public async Task AtMostOnce_ProducerConsumer_ShouldDeliverMessageAtMostOnce()
        {
            // Arrange
            var receivedMessages = new ConcurrentBag<KafkaCheckMessage>();
            int processedCount = 0;

            var testMessages = Enumerable.Range(1, _messageCount)
                .Select(i => new KafkaCheckMessage
                {
                    Topic = _topic,
                    Key = Guid.NewGuid().ToString(),
                    Value = $"AtMostOnce batch message {i}"
                })
                .ToList();

            // Act
            _consumer.Subscribe<KafkaCheckMessage>((message, result, token) =>
            {
                Interlocked.Increment(ref processedCount);
                if (message != null)
                {
                    receivedMessages.Add(message);
                    return Task.CompletedTask;
                }
                else
                    return Task.FromCanceled(new CancellationToken(true));
            });



            await Task.Delay(Timeouts.ConsumerInitialization); // Allow consumer to initialize
            await _producer.PublishAllAsync(_topic, testMessages);
            await _producer.FlushAsync(Timeouts.ProducerFlush);

            // Assert
            await Task.Delay(Timeouts.MessageDelivery); // Allow consumer to initialize
            _logger.Information("Sent {MessageCount} messages, received {ReceivedCount} messages",
                _messageCount, processedCount);
            Assert.True(testMessages.Count == processedCount,
                $"Expected {_messageCount} messages, received {processedCount}");
            Assert.True(testMessages.Count == receivedMessages.Count,
                $"Expected {_messageCount} messages, received {receivedMessages.Count}");
            await Task.Delay(3000);

            await _consumer.UnsubscribeAsync();
        }

        [Fact]
        public async Task AtMostOnce_ProducerBatch_ShouldDeliverAllMessagesAtMostOnce()
        {
            // Arrange
            var receivedMessages = new ConcurrentBag<KafkaCheckMessage<string>>();
            var messagesReceived = new CountdownEvent(_messageCount);
            var producer = _serviceProvider.GetKeyedService<IKafkaProducer>("Test")!;
            var consumer = _serviceProvider.GetKeyedService<IKafkaConsumer>("Test")!;
            var topic = producer.Topics.FirstOrDefault() ?? "Test";

            _disposables.Add(producer);
            _disposables.Add(consumer);

            // Act
            var testMessages = Enumerable.Range(1, _messageCount)
                .Select(i => new KafkaCheckMessage<string>
                {
                    Topic = topic,
                    Key = Guid.NewGuid().ToString(),
                    Value = $"AtMostOnce batch message {i}"
                })
                .ToList();

            consumer.Subscribe<KafkaCheckMessage<string>>((message, result, token) =>
            {
                if (message != null)
                {
                    receivedMessages.Add(message);
                    messagesReceived.Signal();
                    return Task.CompletedTask;
                }
                else
                    return Task.FromCanceled(new CancellationToken(true));
            });

            await Task.Delay(1000);
            await producer.PublishBatchAsync(topic, testMessages);
            await producer.FlushAsync(TimeSpan.FromSeconds(5));

            var allMessagesReceived = messagesReceived.Wait(TimeSpan.FromSeconds(15));

            // Assert
            Assert.True(allMessagesReceived, "All messages should be delivered with AtMostOnce semantics");
            Assert.Equal(_messageCount, receivedMessages.Count);
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
            var receivedMessages = new ConcurrentBag<KafkaCheckMessage>();
            var messagesReceived = new CountdownEvent(_messageCount);
            var producer = _serviceProvider.GetKeyedService<IKafkaProducer>("Test")!;
            var consumer = _serviceProvider.GetKeyedService<IKafkaConsumer>("Test")!;
            var topic = producer.Topics.FirstOrDefault() ?? "Test";

            var testMessages = Enumerable.Range(1, _messageCount)
                .Select(i => new KafkaCheckMessage<string>
                {
                    Topic = topic,
                    Key = Guid.NewGuid().ToString(),
                    Value = $"AtLeastOnce batch message {i}"
                })
                .ToList();

            // Act
            consumer.Subscribe<KafkaCheckMessage>((message, result, token) =>
            {
                if (message != null)
                {
                    receivedMessages.Add(message);
                    messagesReceived.Signal();
                }
                return Task.CompletedTask;
            });

            _disposables.Add(producer);
            _disposables.Add(consumer);
            await Task.Delay(1000);

            await producer.PublishAllAsync(topic, testMessages);
            await producer.FlushAsync(TimeSpan.FromSeconds(5));
            var allMessagesReceived = messagesReceived.Wait(TimeSpan.FromSeconds(30));

            // Assert
            Assert.True(allMessagesReceived, "All messages should be delivered with AtMostOnce semantics");
            Assert.Equal(_messageCount, receivedMessages.Count);

            await consumer.UnsubscribeAsync();
        }

        [Fact]
        public async Task AtLeastOnce_ProducerWithDuplicateDetection_ShouldIncludeMessageId()
        {
            // Arrange
            var receivedHeaders = new ConcurrentBag<Headers>();
            var receivedMessages = new ConcurrentBag<KafkaCheckMessage>();
            var messageIds = new ConcurrentBag<string>();
            var correlationIds = new ConcurrentBag<string>();
            var messagesReceived = new CountdownEvent(_messageCount * 2); // Ожидаем дубликаты

            var producer = _serviceProvider.GetKeyedService<IKafkaProducer>("Test")!;
            var consumer = _serviceProvider.GetKeyedService<IKafkaConsumer>("Test")!;
            var topic = producer.Topics.FirstOrDefault() ?? "Test";

            var testMessage = new KafkaCheckMessage
            {
                Topic = topic,
                Key = Guid.NewGuid().ToString(),
                Value = "AtLeastOnce with duplicate detection"
            };

            // Act
            consumer.Subscribe((result, token) =>
            {
                try
                {
                    receivedHeaders.Add(result.Message.Headers);
                    var message = JsonSerializer.Deserialize<KafkaCheckMessage>(result.Message.Value);
                    if (message != null)
                    {
                        receivedMessages.Add(message);
                    }

                    // fetch meessage ID and correlation IDs from headers
                    if (result.Message.Headers != null)
                    {
                        var correlationHeader = result.Message.Headers
                            .FirstOrDefault(h => h.Key == "correlation-id");
                        if (correlationHeader != null)
                        {
                            var correlationId = Encoding.UTF8.GetString(correlationHeader.GetValueBytes());
                            correlationIds.Add(correlationId);
                        }

                        var messageIdHeader = result.Message.Headers
                            .FirstOrDefault(h => h.Key == "message-id" || h.Key == "idempotence-key");
                        if (messageIdHeader != null)
                        {
                            var messageId = Encoding.UTF8.GetString(messageIdHeader.GetValueBytes());
                            messageIds.Add(messageId);
                        }
                    }

                    messagesReceived.Signal();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error processing message in test");
                }
                return Task.CompletedTask;
            });

            _disposables.Add(producer);
            _disposables.Add(consumer);
            await Task.Delay(1000);

            // simulate duplicates by sending the same message multiple times
            for (int i = 0; i < _messageCount; i++)
            {
                await producer.PublishAsync(topic, testMessage);
                await Task.Delay(100); // delay to allow processing
            }
            // publish one more time to ensure duplicates
            await producer.PublishAsync(topic, testMessage);

            await producer.FlushAsync(TimeSpan.FromSeconds(5));

            var allMessagesReceived = messagesReceived.Wait(TimeSpan.FromSeconds(15));

            // Assert
            Assert.True(allMessagesReceived || receivedMessages.Count >= _messageCount,
                $"Should receive at least {_messageCount} messages but got {receivedMessages.Count}");

            Assert.True(receivedHeaders.Count > 0, "Should have received message headers");

            var headersWithCorrelation = receivedHeaders.Count(h =>
                h.Any(header => header.Key == "correlation-id"));
            var headersWithMessageId = receivedHeaders.Count(h =>
                h.Any(header => header.Key == "message-id" || header.Key == "idempotence-key"));

            Assert.True(headersWithCorrelation > 0 || headersWithMessageId > 0,
                "Messages should include correlation-id or message-id headers for duplicate detection");

            if (producer.GetType().GetProperty("EnableDuplicateDetection")?.GetValue(producer) is bool isDupeDetectionEnabled
                && isDupeDetectionEnabled)
            {
                var uniqueMessageIds = messageIds.Distinct().Count();
                var uniqueCorrelationIds = correlationIds.Distinct().Count();

                Assert.True(uniqueMessageIds > 0 || uniqueCorrelationIds > 0,
                    "Should have unique identifiers for duplicate detection");

                var duplicatesByKey = receivedMessages.GroupBy(m => m.Key).Where(g => g.Count() > 1);
                if (duplicatesByKey.Any())
                {
                    _logger.Information("Found {DuplicateCount} duplicate message groups", duplicatesByKey.Count());
                }
            }

            await consumer.UnsubscribeAsync();
        }

        #endregion

        #region ExactlyOnce Delivery Tests

        [Fact]
        public async Task ExactlyOnce_SingleProducerConsumer_ShouldDeliverMessageExactlyOnce()
        {
            // Arrange
            var producer = _serviceProvider.GetKeyedService<IKafkaProducer>("Test")!;
            var consumer = _serviceProvider.GetKeyedService<IKafkaConsumer>("Test")!;
            var topic = producer.Topics.FirstOrDefault() ?? "Test";
            var receivedMessages = new ConcurrentBag<KafkaCheckMessage>();
            var messageReceivedEvent = new CountdownEvent(_messageCount);

            // Act
            consumer.Subscribe<KafkaCheckMessage>((message, result, token) =>
            {
                if (message != null)
                {
                    receivedMessages.Add(message);
                    messageReceivedEvent.Signal();
                }
                return Task.CompletedTask;
            });

            _disposables.Add(producer);
            _disposables.Add(consumer);
            await Task.Delay(1000);

            var testMessages = new List<KafkaCheckMessage<string>>(_messageCount);
            for (int i = 1; i <= _messageCount; i++)
            {
                testMessages.Add(new KafkaCheckMessage<string>
                {
                    Topic = topic,
                    Key = Guid.NewGuid().ToString(),
                    Value = $"AtLeastOnce batch message {i}"
                });
            }

            await producer.PublishAllAsync(topic, testMessages);
            await producer.FlushAsync(TimeSpan.FromSeconds(5));
            var allMessagesReceived = messageReceivedEvent.Wait(TimeSpan.FromSeconds(30));

            // Assert
            Assert.True(allMessagesReceived, "Message should be delivered");
            await consumer.UnsubscribeAsync();
        }

        [Fact]
        public async Task ExactlyOnce_MultipleProducers_ShouldMaintainTransactionalIntegrity()
        {
            // Arrange
            var producer = _serviceProvider.GetKeyedService<IKafkaProducer>("Test")!;
            var consumer = _serviceProvider.GetKeyedService<IKafkaConsumer>("Test")!;
            var topic = producer.Topics.FirstOrDefault() ?? "Test";
            var receivedMessages = new ConcurrentBag<KafkaCheckMessage>();
            var messagesReceived = new CountdownEvent(_messageCount);

            consumer.Subscribe<KafkaCheckMessage>((message, result, token) =>
            {
                if (message != null)
                {
                    receivedMessages.Add(message);
                    messagesReceived.Signal();
                }
                return Task.CompletedTask;
            });

            _disposables.Add(consumer);
            await Task.Delay(1000);

            // Act - Create multiple producers and send messages concurrently
            var messages = Enumerable.Range(1, _messageCount)
                .Select(i => new KafkaCheckMessage
                {
                    Topic = topic,
                    Key = $"producer-message-{i}",
                    Value = $"ExactlyOnce message {i} from producer"
                })
                .ToList();

            await producer.PublishAllAsync(topic, messages);
            await producer.FlushAsync(TimeSpan.FromSeconds(5));
            var allMessagesReceived = messagesReceived.Wait(TimeSpan.FromSeconds(20));

            // Assert
            Assert.True(allMessagesReceived, "Message should be delivered");
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
            // Arrange
            var producer = _serviceProvider.GetKeyedService<IKafkaProducer>("Test")!;
            var consumer = _serviceProvider.GetKeyedService<IKafkaConsumer>("Test")!;
            var topic = producer.Topics.FirstOrDefault() ?? "Test";
            var receivedMessages = new ConcurrentBag<KafkaCheckMessage?>();
            var processedMessages = new ConcurrentBag<KafkaCheckMessage?>();
            var deathLetterQueueMessages = new ConcurrentBag<KafkaCheckMessage>();
            var message2ErrorCount = 0;
            var messagesReceived = new CountdownEvent(_messageCount);

            var options = _serviceProvider.GetRequiredKeyedService<IOptionsMonitor<ProducerSettings>>("DeadLetter");
            var producerDlq = new KafkaProducer("DeadLetter", options, _logger);

            // Act
            consumer.Subscribe((result, token) =>
            {
                var isError = result.Message.Headers.Any(h => h.Key == "Exception");
                var resultMessage = JsonSerializer.Deserialize<KafkaCheckMessage>(result.Message.Value);

                if (!isError)
                {
                    if (resultMessage != null)
                    {
                        var currentErrorCount = Interlocked.Increment(ref message2ErrorCount);
                        if (currentErrorCount <= 2)
                        {
                            //deathLetterQueueMessages.Add(resultMessage);
                            throw new InvalidOperationException("TestInvalidOperationException");
                        }

                        processedMessages.Add(resultMessage);
                        receivedMessages.Add(resultMessage);
                    }
                    else
                    {
                        _logger.Error("Received null message");
                        return Task.CompletedTask;
                    }
                }
                else
                {
                    if (resultMessage != null)
                    {
                        deathLetterQueueMessages.Add(resultMessage);
                    }
                }

                return Task.CompletedTask;
            });

            _disposables.Add(producer);
            _disposables.Add(consumer);
            await Task.Delay(Timeouts.ConsumerInitialization);

            // Send messages to different partitions using keys
            var messages = Enumerable.Range(1, _messageCount)
                .Select(i => new KafkaCheckMessage
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"Error handling test message {i}"
                })
                .ToList();
            messages[2].Value = null; // Simulate error for message 3

            await producer.PublishAsync(topic, messages);
            await producer.FlushAsync(Timeouts.ProducerFlush);

            // Wait for all messages to be eventually processed
            var timeout = DateTime.UtcNow.Add(TimeSpan.FromSeconds(20));
            while (receivedMessages.Count < _messageCount && DateTime.UtcNow < timeout)
            {
                await Task.Delay(200);
            }

            // Assert
            Assert.NotEqual(_messageCount, receivedMessages.Count);
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
            var producer = _serviceProvider.GetKeyedService<IKafkaProducer>("Test")!;
            var consumer = _serviceProvider.GetKeyedService<IKafkaConsumer>("Test")!;
            var topic = consumer.Topics.FirstOrDefault() ?? "Test";

            var logger = new Mock<ILogger>().Object;
            var receivedMessages = new ConcurrentBag<KafkaCheckMessage>();
            var dlqMessages = new ConcurrentBag<JsonElement>();
            var dlqMessagesReceived = new CountdownEvent(1); // Expecting 1 DLQ message

            // Initialize DLQ if needed
            //var dlqProducerConfig = new CustomProducerConfig
            //{
            //    BootstrapServers = _settings.BootstrapServers,
            //    ClientId = $"{_settings.ClientId}-dlq",
            //    EnableIdempotence = true,
            //    LingerMs = 5,
            //    BatchSize = 16384
            //};

            var producerSettings = _configuration.GetRequiredSection("Kafka:ProducerSettings").Get<ProducerSettings>()!;
            var deadLetterQueue = new KafkaProducer(producerSettings, logger);

            // Act
            consumer.Subscribe(async (result, token) =>
            {
                if (result.Topic == topic && result.Message.Headers.Any(x => x.Key == "Exception"))
                {
                    var ex = new InvalidOperationException("Simulated processing error");
                    await deadLetterQueue.PublishToDeadLetterAsync<KafkaCheckMessage>("DeadLetter", result, ex, 1)
                        .ConfigureAwait(false);

                    receivedMessages.Add(JsonSerializer.Deserialize<KafkaCheckMessage>(result.Message.Value));
                }

                if (result.Topic == "DeadLetter")
                {
                    dlqMessages.Add(JsonSerializer.Deserialize<JsonElement>(result.Message.Value));
                    dlqMessagesReceived.Signal();
                }
            });

            _disposables.Add(producer);
            _disposables.Add(consumer);
            await Task.Delay(Timeouts.ConsumerInitialization);

            // Send success message and error message
            await producer.PublishAsync(topic, new KafkaCheckMessage
            {
                Topic = topic,
                Key = Guid.NewGuid().ToString(),
                Value = "success message"
            });

            await producer.PublishAsync(topic, new KafkaCheckMessage
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
        }

        #endregion

        #region Cleanup

        private async Task ClearKafkaTopicAsync(string topic)
        {
            var adminConfig = new AdminClientConfig
            {
                BootstrapServers = _settings.Common.BootstrapServers
            };

            using var adminClient = new AdminClientBuilder(adminConfig).Build();

            // Delete the topic
            try
            {
                await adminClient.DeleteTopicsAsync([topic]);
                // Wait a bit for deletion to propagate
                await Task.Delay(1000);
            }
            catch (DeleteTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.UnknownTopicOrPart))
            {
                // Topic already deleted, ignore
            }

            // Recreate the topic (with default settings)
            await adminClient.CreateTopicsAsync(new[]
            {
                new TopicSpecification
                {
                    Name = topic,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                }
            });
            await Task.Delay(1000); // Wait for topic to be ready
        }

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

            // Clear Kafka topics used in tests
            var topics = _settings.Consumers.SelectMany(x => x.Topics);
            foreach (var topic in topics)
            {
                ClearKafkaTopicAsync(topic).GetAwaiter().GetResult();
            }
        }

        #endregion
    }

    public class KafkaCheckMessage : IKafkaMessage
    {
        public KafkaCheckMessage()
        {
            Timestamp = new Timestamp(DateTimeOffset.UtcNow);
        }
        public bool IsError { get; set; }
        public string Reason { get; set; } = string.Empty;
        public Offset Offset { get; set; }
        public Partition Partition { get; set; }
        public string Topic { get; set; } = string.Empty;
        public Timestamp Timestamp { get; set; }
        public required string Key { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    public class KafkaCheckMessage<TValue> : KafkaCheckMessage, IKafkaMessage<TValue>
    {
        // Remove 'new' and nullable annotation to match interface signature
        public new TValue Value { get; set; } = default!;
    }
}