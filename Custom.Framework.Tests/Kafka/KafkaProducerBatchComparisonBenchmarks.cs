using BenchmarkDotNet.Attributes;
using Custom.Framework.Kafka;
using Microsoft.VSDiagnostics;
using Moq;

namespace Custom.Framework.Tests.Kafka;

[CPUUsageDiagnoser]
public class KafkaProducerBatchComparisonBenchmarks
{
    private KafkaProducer _producer = null!;
    private List<TestMessage> _messages = null!;
    private ILogger _logger = null!;
    public class TestMessage
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    [Params(10, 50, 100)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _logger = new Mock<ILogger>().Object;
        var otions = new KafkaOptions()
        {
            Common = new CommonSettings
            {
                EnableMetrics = false,
                EnableHealthCheck = false
            },
            Producers = new List<ProducerSettings>
            {
                new ProducerSettings
                {
                    Name = "benchmark-producer",
                    BootstrapServers = "localhost:9092",
                    ClientId = "benchmark-producer",
                    Topics = new[]
                    {
                        "benchmark-topic"
                    },
                    DeliverySemantics = DeliverySemantics.AtMostOnce,
                    EnableMetrics = false,
                    EnableHealthCheck = false,
                    LingerMs = 0,
                    BatchSize = 16 * 1024,
                    MessageMaxBytes = 1 * 1024 * 1024,
                    CompressionType = Confluent.Kafka.CompressionType.None,
                    CompressionLevel = 0
                }
            }
        };
        var deliveryStrategy = DeliveryStrategyFactory.CreateProducerStrategy(
            otions.Producers.First().DeliverySemantics, otions.Producers.First());

        _producer = new KafkaProducer("", otions, deliveryStrategy, _logger);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Regenerate messages for each iteration with the correct MessageCount
        _messages = Enumerable.Range(1, MessageCount).Select(i => new TestMessage { Id = i, Content = $"Message content {i}" }).ToList();
    }

    [Benchmark(Baseline = true)]
    public async Task CurrentApproach_TaskWhenAll()
    {
        var publishTasks = new List<Task>();
        foreach (var message in _messages)
        {
            publishTasks.Add(_producer.PublishAsync("benchmark-topic", message, CancellationToken.None));
        }

        await Task.WhenAll(publishTasks).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task AsyncForeach_Sequential()
    {
        foreach (var message in _messages)
        {
            await _producer.PublishAsync("benchmark-topic", message, CancellationToken.None);
        }
    }

    [Benchmark]
    public async Task AsyncForeach_WithConfigureAwait()
    {
        foreach (var message in _messages)
        {
            await _producer.PublishAsync("benchmark-topic", message, CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Benchmark]
    public async Task ParallelForeach_WithSemaphore()
    {
        using var semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
        var tasks = _messages.Select(async message =>
        {
            await semaphore.WaitAsync();
            try
            {
                await _producer.PublishAsync("benchmark-topic", message, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        });
        await Task.WhenAll(tasks);
    }
}