using BenchmarkDotNet.Attributes;
using Custom.Framework.Kafka;
using Moq;
using Serilog;
using System.Threading.Tasks;

namespace Custom.Framework.Tests.Benchmarks;

[MemoryDiagnoser]
public class KafkaProducerBenchmarks
{
    private KafkaProducer _producer = null!;
    private KafkaMessage _testMessage = null!;
    private ILogger _logger = null!;

    [GlobalSetup]
    public void Setup()
    {
        _logger = new Mock<ILogger>().Object;
        var options = new ProducerOptions
        {
            BootstrapServers = "localhost:9092",
            ClientId = "benchmark-producer",
            DeliverySemantics = DeliverySemantics.AtMostOnce,
            EnableMetrics = false,
            EnableHealthCheck = false,
            LingerMs = 0,
            BatchSize = 16 * 1024,
            MessageMaxBytes = 1 * 1024 * 1024,
            CompressionType = "None",
            CompressionLevel = 0,
            EnableIdempotence = false,
            MaxRetries = 0
        };
        _producer = new KafkaProducer(options, _logger);
        _testMessage = new KafkaMessage
        {
            Topic = "benchmark-topic",
            Key = "benchmark-key",
            Value = "benchmark-value"
        };
    }

    [Benchmark]
    public async Task PublishAsync_Benchmark()
    {
        await _producer.PublishAsync(_testMessage);
    }
}
