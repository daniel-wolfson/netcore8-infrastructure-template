using Confluent.Kafka;
using System.Text.Json;

namespace Custom.Framework.Kafka
{
    public class KafkaMessageSerializer<TMessasge> : ISerializer<TMessasge>
    {
        public byte[] Serialize(TMessasge data, SerializationContext context)
        {
            return JsonSerializer.SerializeToUtf8Bytes(data);
        }
    }
}