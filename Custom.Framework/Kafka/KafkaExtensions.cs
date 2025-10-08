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
            services.AddSingleton<IValidateOptions<KafkaOptions>, KafkaOptionsValidator>();
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
                    KafkaManager.EnsureTopicExists(kafkaOptions.Common.BootstrapServers, topic);
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
                    KafkaManager.EnsureTopicExists(kafkaOptions.Common.BootstrapServers, topic);
                }

                services.AddOptions<ConsumerSettings>(name)
                    .Bind(child)
                    .ValidateDataAnnotations()
                    .Validate(s => s.Topics != null, "Topics required")
                    .ValidateOnStart();

            }

            // Register producer/consumer implementations
            services.AddSingleton<IKafkaFactory, KafkaFactory>();

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

    /// <summary>
        /// Converts ConsumerSettings to Confluent.Kafka.ConsumerConfig
        /// </summary>
        public static ConsumerConfig ToConsumerConfig(this ConsumerSettings settings)
        {
            var config = new ConsumerConfig
            {
                // ClientConfig base properties
                BootstrapServers = settings.BootstrapServers,
                ClientId = settings.ClientId,
                SecurityProtocol = MapSecurityProtocol(settings.SecurityProtocol),
                MetadataMaxAgeMs = settings.MetadataMaxAgeMs,
                SocketTimeoutMs = settings.SocketTimeoutMs,
                SocketKeepaliveEnable = settings.SocketKeepaliveEnable,
                ConnectionsMaxIdleMs = settings.ConnectionsMaxIdleMs,
                ReconnectBackoffMs = settings.ReconnectBackoffMs,
                ReconnectBackoffMaxMs = settings.ReconnectBackoffMaxMs,
                SaslMechanism = MapSaslMechanism(settings.SaslMechanism),
                SaslUsername = settings.SaslUsername,
                SaslPassword = settings.SaslPassword,
                SslCaLocation = settings.SslCaLocation,
                SslCertificateLocation = settings.SslCertificateLocation,
                SslKeyLocation = settings.SslKeyLocation,
                SslKeyPassword = settings.SslKeyPassword,
                EnableSslCertificateVerification = settings.EnableSslCertificateVerification,
                SocketSendBufferBytes = settings.SocketSendBufferBytes,
                SocketReceiveBufferBytes = settings.SocketReceiveBufferBytes,
                LogConnectionClose = settings.LogConnectionClose,

                // ConsumerConfig specific properties
                GroupId = settings.GroupId,
                EnableAutoCommit = settings.EnableAutoCommit,
                AutoCommitIntervalMs = settings.AutoCommitIntervalMs,
                AutoOffsetReset = MapAutoOffsetReset(settings.AutoOffsetReset),
                SessionTimeoutMs = settings.SessionTimeoutMs,
                HeartbeatIntervalMs = settings.HeartbeatIntervalMs,
                MaxPollIntervalMs = settings.MaxPollIntervalMs,
                EnableAutoOffsetStore = settings.EnableAutoOffsetStore,
                FetchMinBytes = settings.FetchMinBytes,
                FetchMaxBytes = settings.FetchMaxBytes,
                MaxPartitionFetchBytes = settings.MaxPartitionFetchBytes,
                FetchWaitMaxMs = settings.FetchWaitMaxMs,
                IsolationLevel = MapIsolationLevel(settings.IsolationLevel),
                CheckCrcs = settings.CheckCrcs,
                GroupInstanceId = settings.GroupInstanceId,
                PartitionAssignmentStrategy = MapPartitionAssignmentStrategy(settings.PartitionAssignmentStrategy)
            };

            // Apply custom consumer config entries (can override above settings)
            if (settings.CustomConsumerConfig != null)
            {
                foreach (var kvp in settings.CustomConsumerConfig)
                {
                    config.Set(kvp.Key, kvp.Value);
                }
            }

            return config;
        }

        private static Confluent.Kafka.SecurityProtocol? MapSecurityProtocol(SecurityProtocol? protocol)
        {
            if (!protocol.HasValue) return null;

            return protocol.Value switch
            {
                SecurityProtocol.Plaintext => Confluent.Kafka.SecurityProtocol.Plaintext,
                SecurityProtocol.Ssl => Confluent.Kafka.SecurityProtocol.Ssl,
                SecurityProtocol.SaslPlaintext => Confluent.Kafka.SecurityProtocol.SaslPlaintext,
                SecurityProtocol.SaslSsl => Confluent.Kafka.SecurityProtocol.SaslSsl,
                _ => Confluent.Kafka.SecurityProtocol.Plaintext
            };
        }

        private static Confluent.Kafka.SaslMechanism? MapSaslMechanism(SaslMechanism? mechanism)
        {
            if (!mechanism.HasValue) return null;

            return mechanism.Value switch
            {
                SaslMechanism.Gssapi => Confluent.Kafka.SaslMechanism.Gssapi,
                SaslMechanism.Plain => Confluent.Kafka.SaslMechanism.Plain,
                SaslMechanism.ScramSha256 => Confluent.Kafka.SaslMechanism.ScramSha256,
                SaslMechanism.ScramSha512 => Confluent.Kafka.SaslMechanism.ScramSha512,
                SaslMechanism.OAuthBearer => Confluent.Kafka.SaslMechanism.OAuthBearer,
                _ => Confluent.Kafka.SaslMechanism.Plain
            };
        }

        private static Confluent.Kafka.AutoOffsetReset? MapAutoOffsetReset(AutoOffsetReset? reset)
        {
            if (!reset.HasValue) return null;

            return reset.Value switch
            {
                AutoOffsetReset.Latest => Confluent.Kafka.AutoOffsetReset.Latest,
                AutoOffsetReset.Earliest => Confluent.Kafka.AutoOffsetReset.Earliest,
                AutoOffsetReset.Error => Confluent.Kafka.AutoOffsetReset.Error,
                _ => Confluent.Kafka.AutoOffsetReset.Latest
            };
        }

        private static Confluent.Kafka.IsolationLevel? MapIsolationLevel(IsolationLevel? level)
        {
            if (!level.HasValue) return null;

            return level.Value switch
            {
                IsolationLevel.ReadUncommitted => Confluent.Kafka.IsolationLevel.ReadUncommitted,
                IsolationLevel.ReadCommitted => Confluent.Kafka.IsolationLevel.ReadCommitted,
                _ => Confluent.Kafka.IsolationLevel.ReadUncommitted
            };
        }

        private static Confluent.Kafka.PartitionAssignmentStrategy? MapPartitionAssignmentStrategy(PartitionAssignmentStrategy? strategy)
        {
            if (!strategy.HasValue) return null;

            return strategy.Value switch
            {
                PartitionAssignmentStrategy.Range => Confluent.Kafka.PartitionAssignmentStrategy.Range,
                PartitionAssignmentStrategy.RoundRobin => Confluent.Kafka.PartitionAssignmentStrategy.RoundRobin,
                PartitionAssignmentStrategy.CooperativeSticky => Confluent.Kafka.PartitionAssignmentStrategy.CooperativeSticky,
                _ => Confluent.Kafka.PartitionAssignmentStrategy.Range
            };
        }
    }
}