using Confluent.Kafka;
using System.Text.Json;

namespace Custom.Framework.Kafka
{
    public class KafkaMessageDeserializer<TMessasge> : IDeserializer<TMessasge>
    {
#pragma warning disable CS8766 // Nullability of reference types in return type doesn't match implicitly implemented member (possibly because of nullability attributes).
        public TMessasge? Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
#pragma warning restore CS8766 // Nullability of reference types in return type doesn't match implicitly implemented member (possibly because of nullability attributes).
        {
            // If data is null or empty, return a new instance to satisfy non-nullable contract
            if (isNull || data.IsEmpty)
            {
                return default;
            }

            var result = JsonSerializer.Deserialize<TMessasge>(data);
            // If deserialization fails, return a new instance to satisfy non-nullable contract
            return result ?? default;
        }
    }
}