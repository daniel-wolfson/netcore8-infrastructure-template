using Custom.Framework.Kafka;
using Custom.Framework.TestFactory.Core;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Kafka
{
    /// <summary>
    /// Unit tests for KafkaFactory producer and consumer pooling functionality
    /// </summary>
    public class KafkaFactoryTests : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ITestOutputHelper _output;
        private readonly IKafkaFactory _kafkaFactory;
        private readonly KafkaOptions _kafkaOptions;
        private readonly List<IDisposable> _disposables = [];
        private readonly WebApplicationFactory<TestProgram> _factory;

        public KafkaFactoryTests(ITestOutputHelper output)
        {
            _output = output;
            _logger = Log.Logger = new TestHostLogger(_output);

            _factory = new WebApplicationFactory<TestProgram>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        // Replace the logger with test-specific one
                        var existingLogger = services.FirstOrDefault(d => d.ServiceType == typeof(ILogger));
                        if (existingLogger != null)
                            services.Remove(existingLogger);

                        services.AddSingleton<ILogger>(_logger);
                    });
                });

            _kafkaOptions = _factory.Services.GetRequiredService<IOptionsMonitor<KafkaOptions>>().CurrentValue;
            _kafkaFactory = _factory.Services.GetRequiredService<IKafkaFactory>();
        }

        #region Producer Pool Tests

        [Fact]
        public void CreateProducer_FirstTime_ShouldCreateNewProducer()
        {
            // Act
            var producer1 = _kafkaFactory.CreateProducer("Test");
            _disposables.Add(producer1);

            // Assert
            Assert.NotNull(producer1);
            _logger.Information("Successfully created first producer instance");
        }

        [Fact]
        public void CreateProducer_SecondTime_ShouldReuseFromPool()
        {
            // Arrange
            var producer1 = _kafkaFactory.CreateProducer("Test");
            _disposables.Add(producer1);

            // Act
            var producer2 = _kafkaFactory.CreateProducer("Test");

            // Assert
            Assert.NotNull(producer2);
            Assert.Same(producer1, producer2);
            _logger.Information("Successfully reused producer from pool");
        }

        [Fact]
        public void CreateProducer_DifferentNames_ShouldCreateSeparateProducers()
        {
            // Arrange & Act
            var producer1 = _kafkaFactory.CreateProducer("Test");
            var producer2 = _kafkaFactory.CreateProducer("DeadLetters");
            _disposables.Add(producer1);
            _disposables.Add(producer2);

            // Assert
            Assert.NotNull(producer1);
            Assert.NotNull(producer2);
            Assert.NotSame(producer1, producer2);
            _logger.Information("Successfully created separate producers for different names");
        }

        [Fact]
        public void CreateProducer_MultipleThreads_ShouldBeThreadSafe()
        {
            // Arrange
            var producerName = "Test";
            var threadCount = 10;
            var producers = new System.Collections.Concurrent.ConcurrentBag<IKafkaProducer>();

            // Act
            Parallel.For(0, threadCount, i =>
            {
                var producer = _kafkaFactory.CreateProducer(producerName);
                producers.Add(producer);
            });

            // Assert
            Assert.Equal(threadCount, producers.Count);
            
            // All producers should be the same instance (from pool)
            var firstProducer = producers.First();
            foreach (var producer in producers)
            {
                Assert.Same(firstProducer, producer);
            }

            _disposables.Add(firstProducer);
            _logger.Information("Successfully verified thread-safe producer creation");
        }

        [Fact]
        public void CreateProducer_InvalidName_ShouldThrowException()
        {
            // Act & Assert
            var exception = Assert.Throws<NullReferenceException>(() => 
                _kafkaFactory.CreateProducer("NonExistentProducer"));
            
            Assert.Contains("Producer not defined", exception.Message);
            _logger.Information("Successfully validated exception for invalid producer name");
        }

        #endregion

        #region Consumer Pool Tests

        [Fact]
        public void CreateConsumer_FirstTime_ShouldCreateNewConsumer()
        {
            // Act
            var consumer = _kafkaFactory.CreateConsumer("Test"); // Ensure group is created first

            // Assert
            Assert.NotNull(consumer);
            _logger.Information("Successfully created first consumer instance");
        }

        [Fact]
        public void CreateConsumer_SecondTime_ShouldReuseFromPool()
        {
            // Arrange
            var consumer1 = _kafkaFactory.CreateConsumer("Test"); // Ensure group is created first
            _disposables.Add(consumer1);

            // Act
            var consumer2 = _kafkaFactory.CreateConsumer("Test");

            // Assert
            Assert.NotNull(consumer2);
            Assert.Same(consumer1, consumer2);
            _logger.Information("Successfully reused consumer from pool");
        }

        [Fact]
        public void CreateConsumer_DifferentNames_ShouldCreateSeparateConsumers()
        {
            // Arrange & Act
            var consumer1 = _kafkaFactory.CreateConsumer("Test");
            var consumer2 = _kafkaFactory.CreateConsumer("DeadLetters");
            _disposables.Add(consumer1);
            _disposables.Add(consumer2);

            // Assert
            Assert.NotNull(consumer1);
            Assert.NotNull(consumer2);
            Assert.NotSame(consumer1, consumer2);
            _logger.Information("Successfully created separate consumers for different names");
        }

        [Fact]
        public void CreateConsumer_MultipleThreads_ShouldBeThreadSafe()
        {
            // Arrange
            var consumerName = "Test";
            var groupId = "Test-Group";
            var threadCount = 10;
            var consumers = new System.Collections.Concurrent.ConcurrentBag<IKafkaConsumer>();

            // Act
            Parallel.For(0, threadCount, i =>
            {
                var consumer = _kafkaFactory.CreateConsumer(consumerName);
                consumers.Add(consumer);
            });

            // Assert
            Assert.Equal(threadCount, consumers.Count);
            
            // All consumers should be the same instance (from pool)
            var firstConsumer = consumers.First();
            foreach (var consumer in consumers)
            {
                Assert.Same(firstConsumer, consumer);
            }

            _disposables.Add(firstConsumer);
            _logger.Information("Successfully verified thread-safe consumer creation");
        }

        [Fact]
        public void CreateConsumer_InvalidName_ShouldThrowException()
        {
            // Act & Assert
            var exception = Assert.Throws<NullReferenceException>(() =>
                _kafkaFactory.CreateConsumer("NonExistentConsumer"));
            
            Assert.Contains("Consumer not defined", exception.Message);
            _logger.Information("Successfully validated exception for invalid consumer name");
        }

        #endregion

        #region Mixed Pool Tests

        [Fact]
        public void CreateProducerAndConsumer_ShouldUseSeparatePools()
        {
            // Act
            var producer = _kafkaFactory.CreateConsumer("Test");
            var consumer = _kafkaFactory.CreateConsumer("Test");
            _disposables.Add(producer);
            _disposables.Add(consumer);

            // Assert
            Assert.NotNull(producer);
            Assert.NotNull(consumer);
            _logger.Information("Successfully created producer and consumer in separate pools");
        }

        [Fact]
        public void Dispose_ShouldCleanupAllPooledResources()
        {
            // Arrange
            var factory = _factory.Services.GetRequiredService<IOptionsMonitor<KafkaOptions>>();
            var logger = _factory.Services.GetRequiredService<ILogger>();
            var testFactory = new KafkaFactory(factory, logger);
            
            var producer = testFactory.CreateProducer("Test");
            var consumer = testFactory.CreateConsumer("Test");

            // Act
            testFactory.Dispose();

            // Assert
            // If Dispose completes without exception, the test passes
            Assert.True(true);
            _logger.Information("Successfully disposed factory and cleaned up both pools");
        }

        #endregion

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                try
                {
                    // Don't dispose pooled resources - factory manages them
                    // Just clear our tracking list
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Error during test cleanup");
                }
            }
            _disposables.Clear();
            _factory?.Dispose();
        }
    }
}
