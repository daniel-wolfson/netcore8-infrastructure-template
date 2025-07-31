using System.Diagnostics.Metrics;

namespace Custom.Framework.Kafka
{
    public class KafkaMetrics
    {
        private readonly Meter _meter;
        private readonly Counter<long> _messagesSent;
        private readonly Counter<long> _messagesReceived;
        private readonly Counter<long> _errors;
        private readonly Histogram<double> _producerLatency;
        private readonly Histogram<double> _consumerLatency;

        public KafkaMetrics(string clientId)
        {
            _meter = new Meter($"Custom.Kafka.{clientId}");
            
            _messagesSent = _meter.CreateCounter<long>("kafka.producer.messages",
                description: "Number of messages sent");
                
            _messagesReceived = _meter.CreateCounter<long>("kafka.consumer.messages",
                description: "Number of messages received");
                
            _errors = _meter.CreateCounter<long>("kafka.errors",
                description: "Number of errors");
                
            _producerLatency = _meter.CreateHistogram<double>("kafka.producer.latency",
                unit: "ms",
                description: "Producer latency in milliseconds");
                
            _consumerLatency = _meter.CreateHistogram<double>("kafka.consumer.latency",
                unit: "ms",
                description: "Consumer processing latency in milliseconds");
        }

        public void RecordMessageSent()
        {
            _messagesSent.Add(1);
        }

        public void RecordMessageReceived()
        {
            _messagesReceived.Add(1);
        }

        public void RecordError()
        {
            _errors.Add(1);
        }

        public void RecordProducerLatency(TimeSpan duration)
        {
            _producerLatency.Record(duration.TotalMilliseconds);
        }

        public void RecordConsumerLatency(TimeSpan duration)
        {
            _consumerLatency.Record(duration.TotalMilliseconds);
        }
    }
}