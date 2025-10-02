using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Custom.Framework.Kafka
{
    public static class KafkaExtensions
    {
        /// <summary>
        /// Adds Kafka producer and consumer services, configuration, and OpenTelemetry instrumentation to the specified
        /// service collection.
        /// </summary>
        public static IServiceCollection AddKafka(this IServiceCollection services, IConfiguration configuration)
        {
            var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
            var kafkaSection = configuration.GetSection("Kafka");
            services.AddSingleton<IValidateOptions<KafkaOptions>, KafkaoptionsValidator>();
            services.AddOptions<KafkaOptions>("Kafka").Bind(kafkaSection);

            var kafkaOptions = configuration.GetSection("Kafka").Get<KafkaOptions>()
                ?? throw new ArgumentNullException("Kafka:Common section is not defined");
            ArgumentNullException.ThrowIfNull(kafkaOptions.Common, "Kafka:Common is required");
            ArgumentNullException.ThrowIfNull(kafkaOptions.Producers, "Kafka:Producers is required");
            ArgumentNullException.ThrowIfNull(kafkaOptions.Consumers, "Kafka:Consumers is required");

            var producers = configuration.GetSection("Kafka:Producers");
            foreach (var child in producers.GetChildren())
            {
                var name = child.GetValue<string>("Name");
                if (string.IsNullOrWhiteSpace(name))
                    throw new InvalidOperationException("Each Kafka:Producers item must have a Name.");

                var topics = child.GetSection("Topics").Get<string[]>() ?? [];
                foreach (var topic in topics)
                {
                    KafkaTopicManager.EnsureTopicExists(kafkaOptions.Common.BootstrapServers, topic);
                }

                services.AddOptions<ProducerSettings>(name)
                    .Bind(child)
                    .ValidateDataAnnotations()
                    .Validate(s => s.Topics != null, "Topics required")
                    .ValidateOnStart();

                //if (producerSettings.EnableHealthCheck)
                //{
                //    // Use provided timeout or a sensible default
                //    var timeout = producerSettings.HealthCheckTimeout ?? TimeSpan.FromSeconds(5);
                //    services.AddHealthChecks()
                //        .AddCheck<KafkaHealthCheck>("kafka", timeout: timeout);
                //}
            }

            var consumers = configuration.GetSection("Kafka:Consumers");
            foreach (var child in consumers.GetChildren())
            {
                var name = child.GetValue<string>("Name");
                if (string.IsNullOrWhiteSpace(name))
                    throw new InvalidOperationException("Each Kafka:Producers item must have a Name.");

                var topics = child.GetSection("Topics").Get<string[]>() ?? [];
                foreach (var topic in topics)
                {
                    KafkaTopicManager.EnsureTopicExists(kafkaOptions.Common.BootstrapServers, topic);
                }

                services.AddOptions<ConsumerSettings>(name)
                    .Bind(child)
                    .ValidateDataAnnotations()
                    .Validate(s => s.Topics != null, "Topics required")
                    .ValidateOnStart();

            }

            // Register producer/consumer implementations
            services.AddSingleton(typeof(IKafkaProducer), typeof(KafkaProducer));
            services.AddSingleton(typeof(IKafkaConsumer), typeof(KafkaConsumer));

            // Add OpenTelemetry instrumentation
            services.AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddSource("Kafka.Producer")
                    .AddSource("Kafka.Consumer"));

            return services;
        }
   
        public static string ToListString(this List<TopicPartition> partitions)
        {
            return string.Join(",", partitions.Select(p => $"{p.Topic}:{p.Partition}"));
        }
    }
}