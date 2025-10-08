namespace Custom.Framework.Kafka
{
    public interface IKafkaFactory : IDisposable
    {
        IKafkaProducer CreateProducer(string name);
        IKafkaConsumer CreateConsumer(string name);
    }
}