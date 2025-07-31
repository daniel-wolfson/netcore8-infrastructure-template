using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Custom.Framework.Kafka
{
    public static class KafkaExtensions
    {
        public static IServiceCollection AddKafka(this IServiceCollection services, IConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var commonSection = configuration.GetSection("Kafka");
            var producerSection = configuration.GetSection("Kafka:Producer");
            var consumerSection = configuration.GetSection("Kafka:Consumer");

            // Build merged settings instances (common values first, then specific overrides)
            var producerSettings = new ProducerSettings();
            commonSection.Bind(producerSettings);
            producerSection.Bind(producerSettings);

            var consumerSettings = new ConsumerSettings();
            commonSection.Bind(consumerSettings);
            consumerSection.Bind(consumerSettings);

            // Register IConfiguration binding for consumers via IOptions<T>
            // Apply common section first, then specific overrides so specific keys win
            services.Configure<ProducerSettings>(commonSection);
            services.Configure<ProducerSettings>(producerSection);

            services.Configure<ConsumerSettings>(commonSection);
            services.Configure<ConsumerSettings>(consumerSection);

            // Register concrete merged settings for direct injection (optional but convenient)
            //services.AddSingleton(producerSettings);
            //services.AddSingleton(consumerSettings);

            // Register producer/consumer implementations
            services.AddSingleton(typeof(IKafkaProducer<>), typeof(KafkaProducer<>));
            services.AddSingleton(typeof(IKafkaConsumer), typeof(KafkaConsumer));

            if (producerSettings.EnableHealthCheck)
            {
                // Use provided timeout or a sensible default
                var timeout = producerSettings.HealthCheckTimeout ?? TimeSpan.FromSeconds(5);
                services.AddHealthChecks()
                    .AddCheck<KafkaHealthCheck>("kafka", timeout: timeout);
            }

            // Add OpenTelemetry instrumentation
            services.AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddSource("Custom.Kafka.Producer")
                    .AddSource("Custom.Kafka.Consumer"));

            return services;
        }
    }
}