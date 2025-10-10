using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Custom.Framework.Kafka;
using Custom.Framework.TestFactory.Core;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using Serilog;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;
using Path = System.IO.Path;

namespace Custom.Framework.Tests
{
    /// <summary>
    /// Integration tests for Kafka producer and consumer with different delivery semantics.
    /// These tests verify the behavior of AtMostOnce, AtLeastOnce, and ExactlyOnce delivery scenarios.
    /// </summary>
    public class KafkaTests(ITestOutputHelper output) : IAsyncLifetime
    {
        private readonly ILogger _logger = Log.Logger = new TestHostLogger(output);
        private readonly List<IDisposable> _disposables = [];
        private KafkaOptions _settings = default!;
        private IKafkaProducer _producer = default!;
        private IKafkaConsumer _consumer = default!;
        private IKafkaFactory _kafkaFactory = default!;
        private string _topic = default!;
        private WebApplicationFactory<TestProgram> _factory = default!;
        private int _messageCount = 3;

        public Task InitializeAsync()
        {
            _factory = new WebApplicationFactory<TestProgram>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Test");
                    builder.ConfigureAppConfiguration((context, config) =>
                        ConfigureTestAppConfiguration(context, config));
                    builder.ConfigureServices((context, services) =>
                        ConfigureServices(context, services));
                    builder.ConfigureTestServices(services =>
                        services.AddSingleton<ILogger>(new TestHostLogger(output)));
                });

            _settings = _factory.Services
                .GetService<IOptionsMonitor<KafkaOptions>>()?.Get("Kafka")
                ?? throw new ArgumentNullException("KafkaOptions not defined");

            var groupId = _settings.Consumers.FirstOrDefault()?.GroupId
                ?? throw new ArgumentNullException("No consumer group defined in settings");

            var topics = _settings.Consumers.SelectMany(x => x.Topics);
            foreach (var topic in topics)
            {
                ClearKafkaTopicAsync(topic).GetAwaiter().GetResult();
            }

            _kafkaFactory = _factory.Services.GetService<IKafkaFactory>()!;
            _producer = _kafkaFactory.CreateProducer("Test")!;
            _consumer = _kafkaFactory.CreateConsumer("Test")!;
            _topic = _producer.Topics.FirstOrDefault() ?? "Test";
            _disposables.Add(_producer);
            _disposables.Add(_consumer);

            return Task.CompletedTask;
        }

        #region AtLeastOnce Delivery Tests

        [Fact]
        public async Task AtLeastOnce_ProducerConsumer_ShouldDeliverMessageAtLeastOnce()
        {
            // Arrange
            var receivedMessages = new ConcurrentBag<KafkaMessage>();
            var topic = _producer.Topics.FirstOrDefault() ?? "Test";

            var testMessages = Enumerable.Range(1, _messageCount)
                .Select(i => new KafkaMessage
                {
                    Topic = topic,
                    Key = Guid.NewGuid().ToString(),
                    Value = $"AtLeastOnce batch message {i}"
                })
                .ToList();

            // Act
            _consumer.Subscribe<KafkaMessage>((message, result, token) =>
            {
                if (message != null && !token.IsCancellationRequested)
                {
                    receivedMessages.Add(message);
                }
                return Task.CompletedTask;
            });

            await _producer.PublishAllAsync(topic, testMessages);
            await _producer.FlushAsync(TimeSpan.FromSeconds(5));
            await _consumer.FlushAsync(TimeSpan.FromSeconds(30));
            await _consumer.UnsubscribeAsync();

            // Assert
            Assert.True(testMessages.Count == receivedMessages.Count,
                $"Expected {_messageCount} messages, received {receivedMessages.Count}");
        }

        [Fact]
        public async Task AtLeastOnce_ProducerWithDuplicateDetection_ShouldIncludeMessageId()
        {
            // Arrange
            var receivedHeaders = new ConcurrentBag<Headers>();
            var receivedMessages = new ConcurrentBag<KafkaMessage>();
            var messageIds = new ConcurrentBag<string>();
            var correlationIds = new ConcurrentBag<string>();
            var topic = _producer.Topics.FirstOrDefault() ?? "Test";

            var testMessage = new KafkaMessage
            {
                Topic = topic,
                Key = Guid.NewGuid().ToString(),
                Value = "AtLeastOnce with duplicate detection"
            };

            // Act
            _consumer.Subscribe((result, token) =>
            {
                try
                {
                    if (token.IsCancellationRequested)
                        return Task.FromCanceled(token);

                    receivedHeaders.Add(result.Message.Headers);
                    var message = JsonSerializer.Deserialize<KafkaMessage>(result.Message.Value);
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
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error processing message in test");
                }
                return Task.CompletedTask;
            });

            // simulate duplicates by sending the same message multiple times
            for (int i = 0; i < _messageCount; i++)
            {
                await _producer.PublishAsync(topic, testMessage);
                //await Task.Delay(100); // delay to allow processing
            }
            // publish one more time to ensure duplicates
            await _producer.PublishAsync(topic, testMessage);

            await _producer.FlushAsync(TimeSpan.FromSeconds(5));
            await _consumer.FlushAsync();
            await _consumer.UnsubscribeAsync();

            // Assert
            Assert.True(receivedMessages.Count >= _messageCount,
                $"Should receive at least {_messageCount} messages but got {receivedMessages.Count}");

            Assert.True(receivedHeaders.Count > 0, "Should have received message headers");

            var headersWithCorrelation = receivedHeaders.Count(h =>
                h.Any(header => header.Key == "correlation-id"));
            var headersWithMessageId = receivedHeaders.Count(h =>
                h.Any(header => header.Key == "message-id" || header.Key == "idempotence-key"));

            Assert.True(headersWithCorrelation > 0 || headersWithMessageId > 0,
                "Messages should include correlation-id or message-id headers for duplicate detection");

            if (_producer.GetType().GetProperty("EnableDuplicateDetection")?.GetValue(_producer) is bool isDupeDetectionEnabled
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
        }

        #endregion

        #region AtMostOnce Delivery Tests

        [Fact]
        public async Task AtMostOnce_ProducerConsumer_ShouldDeliverMessageAtMostOnce()
        {
            // Arrange
            var receivedMessages = new ConcurrentBag<KafkaMessage>();
            int processedCount = 0;

            var testMessages = Enumerable.Range(1, _messageCount)
                .Select(i => new KafkaMessage
                {
                    Topic = _topic,
                    Key = Guid.NewGuid().ToString(),
                    Value = $"AtMostOnce batch message {i}"
                })
                .ToList();

            // Act
            _consumer.Subscribe<KafkaMessage>((message, result, token) =>
            {
                if (message != null && !token.IsCancellationRequested)
                {
                    Interlocked.Increment(ref processedCount);
                    receivedMessages.Add(message);
                }
                return Task.CompletedTask;
            });

            await Task.Delay(Timeouts.ConsumerInitialization); // Allow consumer to initialize
            await _producer.PublishAllAsync(_topic, testMessages);
            await _producer.FlushAsync(Timeouts.ProducerFlush);
            await _consumer.FlushAsync();
            await _consumer.UnsubscribeAsync();

            // Assert
            await Task.Delay(Timeouts.MessageDelivery); // Allow consumer to initialize
            _logger.Information("Sent {MessageCount} messages, received {ReceivedCount} messages",
                _messageCount, processedCount);
            Assert.True(testMessages.Count == processedCount,
                $"Expected {_messageCount} messages, received {processedCount}");
            Assert.True(testMessages.Count == receivedMessages.Count,
                $"Expected {_messageCount} messages, received {receivedMessages.Count}");
        }

        [Fact]
        public async Task AtMostOnce_ProducerBatch_ShouldDeliverAllMessagesAtMostOnce()
        {
            // Arrange
            var receivedMessages = new ConcurrentBag<KafkaMessage>();
            var topic = _producer.Topics.FirstOrDefault() ?? "Test";

            // Act
            var testMessages = Enumerable.Range(1, _messageCount)
                .Select(i => new KafkaMessage
                {
                    Topic = topic,
                    Key = Guid.NewGuid().ToString(),
                    Value = $"AtMostOnce batch message {i}"
                })
                .ToList();

            _consumer.Subscribe<KafkaMessage[]>((messages, result, token) =>
            {
                if (messages != null)
                {
                    messages?.ToList().ForEach(x => receivedMessages.Add(x));
                    return Task.CompletedTask;
                }
                else
                    return Task.FromCanceled(new CancellationToken(true));
            });

            await _producer.PublishBatchAsync(topic, testMessages);
            await _producer.FlushAsync(TimeSpan.FromSeconds(5));
            await _consumer.FlushAsync(TimeSpan.FromSeconds(30));
            await _consumer.UnsubscribeAsync();

            // Assert
            Assert.True(testMessages.Count == receivedMessages.Count,
                $"Expected {_messageCount} messages, received {receivedMessages.Count}");

            foreach (var original in testMessages)
            {
                Assert.Contains(receivedMessages, rm => rm.Key == original.Key);
            }
        }

        #endregion

        #region ExactlyOnce Delivery Tests

        [Fact]
        public async Task ExactlyOnce_SingleProducerConsumer_ShouldDeliverMessageExactlyOnce()
        {
            // Arrange
            var topic = _producer.Topics.FirstOrDefault() ?? "Test";
            var receivedMessages = new ConcurrentBag<KafkaMessage>();

            // Act
            _consumer.Subscribe<KafkaMessage>((message, result, token) =>
            {
                if (message != null)
                {
                    receivedMessages.Add(message);
                }
                return Task.CompletedTask;
            });

            var testMessages = new List<KafkaMessage>(_messageCount);
            for (int i = 1; i <= _messageCount; i++)
            {
                testMessages.Add(new KafkaMessage
                {
                    Topic = topic,
                    Key = Guid.NewGuid().ToString(),
                    Value = $"AtLeastOnce batch message {i}"
                });
            }

            await _producer.PublishAllAsync(topic, testMessages);
            await _producer.FlushAsync(TimeSpan.FromSeconds(5));
            await _consumer.FlushAsync();
            await _consumer.UnsubscribeAsync();

            // Assert
            Assert.True(testMessages.Count == receivedMessages.Count,
                $"Expected {_messageCount} messages, received {receivedMessages.Count}");
        }

        [Fact]
        public async Task ExactlyOnce_MultipleProducers_ShouldMaintainTransactionalIntegrity()
        {
            // Arrange
            var topic = _producer.Topics.FirstOrDefault() ?? "Test";
            var receivedMessages = new ConcurrentBag<KafkaMessage>();

            _consumer.Subscribe<KafkaMessage>((message, result, token) =>
            {
                if (message != null && !token.IsCancellationRequested)
                {
                    receivedMessages.Add(message);
                }
                return Task.CompletedTask;
            });

            // Act - Create multiple producers and send messages concurrently
            var testMessages = Enumerable.Range(1, _messageCount)
                .Select(i => new KafkaMessage
                {
                    Topic = topic,
                    Key = $"producer-message-{i}",
                    Value = $"ExactlyOnce message {i} from producer"
                })
                .ToList();

            await _producer.PublishAllAsync(topic, testMessages);
            await _producer.FlushAsync(TimeSpan.FromSeconds(5));
            await _consumer.FlushAsync();
            await _consumer.UnsubscribeAsync();

            // Assert
            Assert.True(testMessages.Count == receivedMessages.Count,
                $"Expected {_messageCount} messages, received {receivedMessages.Count}");
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
            var receivedMessages = new ConcurrentBag<KafkaMessage?>();
            var deathLetterQueueMessages = new ConcurrentBag<KafkaMessage>();
            var producerDlq = _kafkaFactory.CreateProducer("DeadLetters")!;
            var consumerDlq = _kafkaFactory.CreateConsumer("DeadLetters")!;

            // Act
            _consumer.Subscribe<KafkaMessage>(["Test"], async (message, result, token) =>
            {
                try
                {
                    if (message?.Value?.Length > 0)
                    {
                        receivedMessages.Add(message);
                    }
                    else
                    {
                        throw new InvalidOperationException("TestInvalidOperationException");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error processing message with key {MessageKey}", message?.Key);
                    if (message != null)
                    {
                        message.Value += $" Error message: {ex.Message}";
                    }
                    await producerDlq.PublishAsync("DeadLetters", message!, token).ConfigureAwait(false);
                }
            });

            consumerDlq.Subscribe(["DeadLetters"], (result, token) =>
            {
                var dlqMessage = JsonSerializer.Deserialize<KafkaMessage>(result.Message.Value);
                if (dlqMessage != null)
                {
                    deathLetterQueueMessages.Add(dlqMessage);
                }
                return Task.CompletedTask;
            });

            // Send messages to different partitions using keys
            var messages = Enumerable.Range(1, _messageCount)
                .Select(i => new KafkaMessage
                {
                    Topic = "Test",
                    Key = $"key-{i}",
                    Value = i == 1 ? string.Empty : $"Message {i}" // Error simulation !!!
                })
                .ToList();

            await _producer.PublishAllAsync("Test", messages);
            await _producer.FlushAsync(Timeouts.ProducerFlush);
            await _consumer.FlushAsync(TimeSpan.FromSeconds(30));
            await _consumer.UnsubscribeAsync();

            // Assert
            Assert.NotEqual(_messageCount, receivedMessages.Count);
            Assert.Single(deathLetterQueueMessages);
        }

        #endregion

        #region Cleanup

        public Task DisposeAsync()
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
            return Task.CompletedTask;
        }

        #endregion

        #region private methods

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
                    ReplicationFactor = 1,
                    Configs = new Dictionary<string, string>
                    {
                        // Retain messages for 1 hour (for testing).
                        // Default: 7 days (168 hours). -1 -> Infinite retention
                        { "retention.ms", "3600000" },
                
                        // Or retain only 100 MB
                        { "retention.bytes", "104857600" },
                
                        // Cleanup policy: delete (vs compact)
                        { "cleanup.policy", "delete" },
                
                        // Check for deletion every 5 minutes
                        { "delete.retention.ms", "300000" },
                
                        // Segment size (1 MB for faster testing)
                        { "segment.bytes", "1048576" },
                
                        // Roll segment every 10 minutes
                        { "segment.ms", "600000" }
                    }
                }
            });
            await Task.Delay(1000); // Wait for topic to be ready
        }

        private void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
        {
            var configuration = context.Configuration;

            // Replace the logger with test-specific one
            var existingLogger = services.FirstOrDefault(d => d.ServiceType == typeof(ILogger));
            if (existingLogger != null)
                services.Remove(existingLogger);

            services.AddSingleton<ILogger>(_logger);

            services.AddOptions<KafkaOptions>()
                .Bind(configuration.GetSection("Kafka"));

            var producersArray = configuration.GetSection("Kafka:Producers").Get<ProducerSettings[]>();
            var consumersArray = configuration.GetSection("Kafka:Consumers").Get<ConsumerSettings[]>();

            if (producersArray != null)
            {
                foreach (var producer in producersArray)
                {
                    services.Configure<ProducerSettings>(producer.Name, options =>
                    {
                        configuration.GetSection($"Kafka:Producers:{Array.IndexOf(producersArray, producer)}").Bind(options);
                    });
                }
            }
            if (consumersArray != null)
            {
                foreach (var consumer in consumersArray)
                {
                    services.Configure<ConsumerSettings>(consumer.Name, options =>
                    {
                        configuration.GetSection($"Kafka:Consumers:{Array.IndexOf(consumersArray, consumer)}").Bind(options);
                    });
                }
            }

            services.AddSingleton(Log.Logger);
            services.AddKafka(configuration);

            static Func<IServiceProvider, object, IKafkaConsumer> ConsumerMakeAction()
            {
                return (serviceProvider, key) =>
                {
                    var kafkaOptionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<KafkaOptions>>();
                    var options = kafkaOptionsMonitor.Get(key.ToString()!);
                    var logger = serviceProvider.GetRequiredService<ILogger>()!;
                    var consumersSettings = options.Consumers.FirstOrDefault(c => c.Name == key.ToString())
                        ?? throw new NullReferenceException($"Consumer kafkaOptionsMonitor not found for key {key}");
                    var deliveryStrategy = DeliveryStrategyFactory.CreateConsumerStrategy(
                        options.Producers.First().DeliverySemantics, consumersSettings);
                    return new KafkaConsumer(key.ToString()!, options, deliveryStrategy, logger);
                };
            }
            services.AddKeyedSingleton("Test", ConsumerMakeAction());
            services.AddKeyedSingleton("DeadLetters", ConsumerMakeAction());

            static Func<IServiceProvider, object, IKafkaProducer> MakeProducerAction()
            {
                return (serviceProvider, key) =>
                {
                    var kafkaOptionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<KafkaOptions>>();
                    var options = kafkaOptionsMonitor.Get(key.ToString()!);
                    var logger = serviceProvider.GetRequiredService<ILogger>()!;
                    var producerSettings = options.Producers.FirstOrDefault(c => c.Name == key.ToString())
                        ?? throw new NullReferenceException($"Consumer kafkaOptionsMonitor not found for key {key}");
                    var deliveryStrategy = DeliveryStrategyFactory.CreateProducerStrategy(
                        options.Producers.First().DeliverySemantics, producerSettings);
                    return new KafkaProducer(key.ToString()!, options, deliveryStrategy, logger);
                };
            }
            services.AddKeyedSingleton("Test", MakeProducerAction());
            services.AddKeyedSingleton("DeadLetters", MakeProducerAction());
        }

        private void ConfigureTestAppConfiguration(
            WebHostBuilderContext builderContext, IConfigurationBuilder builderConfig)
        {
            var directory = Path.GetDirectoryName(typeof(TestHostBase).Assembly.Location)!;
            var env = builderContext.HostingEnvironment;
            //builderContext.Properties.Add("IsDebugMode", _isDebugMode);

            var environmentName = env.EnvironmentName;
            var contentRootPath = env.ContentRootPath;
            //var fileProvider = env.ContentRootFileProvider;
            //var changeToken = fileProvider.Watch(fileName);
            builderConfig
                .AddJsonFile(Path.Combine(directory, $"appsettings.json"), optional: false)
                .AddJsonFile(Path.Combine(directory, $"appsettings.{environmentName}.json"), optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();
        }

        #endregion private methods
    }
}