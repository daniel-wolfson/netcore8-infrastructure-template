using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// Utility class for managing Kafka topics programmatically.
    /// Useful for ensuring topics exist before starting producers/consumers.
    /// </summary>
    public static partial class KafkaManager
    {
        public static bool EnsureTopicExists(
                    string bootstrapServers,
                    string topicName,
                    int numPartitions = 1,
                    short replicationFactor = 1,
                    TimeSpan? timeout = null)
        {
            return EnsureTopicExistsAsync(bootstrapServers, topicName, numPartitions, replicationFactor, timeout)
                .GetAwaiter().GetResult();
        }

        /// <summary>
        /// Creates a Kafka topic if it doesn't already exist.
        /// </summary>
        /// <param name="bootstrapServers">Kafka bootstrap servers (e.g., "localhost:9092")</param>
        /// <param name="topicName">Name of the topic to create</param>
        /// <param name="numPartitions">Number of partitions (default: 1)</param>
        /// <param name="replicationFactor">Replication factor (default: 1)</param>
        /// <param name="timeout">Timeout for topic creation (default: 30 seconds)</param>
        /// <returns>True if topic was created or already exists, false if creation failed</returns>
        public static async Task<bool> EnsureTopicExistsAsync(
            string bootstrapServers,
            string topicName,
            int numPartitions = 1,
            short replicationFactor = 1,
            TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(bootstrapServers))
                throw new ArgumentException("Bootstrap servers cannot be null or empty", nameof(bootstrapServers));
                
            if (string.IsNullOrWhiteSpace(topicName))
                throw new ArgumentException("Name name cannot be null or empty", nameof(topicName));

            var adminConfig = new AdminClientConfig
            {
                BootstrapServers = bootstrapServers
            };

            using var adminClient = new AdminClientBuilder(adminConfig).Build();

            try
            {
                // Check if topic already exists
                var metadata = adminClient.GetMetadata(timeout ?? TimeSpan.FromSeconds(30));
                if (metadata.Topics.Any(t => t.Topic == topicName && t.Error.Code == ErrorCode.NoError))
                {
                    return true; // Name already exists
                }

                // Create the topic
                var topicSpec = new TopicSpecification
                {
                    Name = topicName,
                    NumPartitions = numPartitions,
                    ReplicationFactor = replicationFactor
                };

                adminClient.CreateTopicsAsync(new[] { topicSpec }, new CreateTopicsOptions
                {
                    RequestTimeout = timeout ?? TimeSpan.FromSeconds(30)
                }).GetAwaiter().GetResult();

                return true;
            }
            catch (CreateTopicsException ex)
            {
                // If topic already exists, that's considered success
                if (ex.Results?.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists) == true)
                {
                    return true;
                }

                // Re-throw for other errors
                throw;
            }
        }

        /// <summary>
        /// Creates multiple Kafka topics if they don't already exist.
        /// </summary>
        /// <param name="bootstrapServers">Kafka bootstrap servers (e.g., "localhost:9092")</param>
        /// <param name="topicConfigurations">Name configurations to create</param>
        /// <param name="timeout">Timeout for topic creation (default: 30 seconds)</param>
        /// <returns>Dictionary with topic names and their creation success status</returns>
        public static async Task<Dictionary<string, bool>> EnsureTopicsExistAsync(
            string bootstrapServers,
            IEnumerable<KafkaTopicConfiguration> topicConfigurations,
            TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(bootstrapServers))
                throw new ArgumentException("Bootstrap servers cannot be null or empty", nameof(bootstrapServers));

            var configs = topicConfigurations?.ToList() ?? throw new ArgumentNullException(nameof(topicConfigurations));
            if (!configs.Any())
                return new Dictionary<string, bool>();

            var adminConfig = new AdminClientConfig
            {
                BootstrapServers = bootstrapServers
            };

            using var adminClient = new AdminClientBuilder(adminConfig).Build();
            var results = new Dictionary<string, bool>();

            try
            {
                // Check which topics already exist
                var metadata = adminClient.GetMetadata(timeout ?? TimeSpan.FromSeconds(30));
                var existingTopics = metadata.Topics
                    .Where(t => t.Error.Code == ErrorCode.NoError)
                    .Select(t => t.Topic)
                    .ToHashSet();

                // Separate existing and new topics
                var topicsToCreate = configs
                    .Where(config => !existingTopics.Contains(config.Name))
                    .ToList();

                // Mark existing topics as successful
                foreach (var config in configs.Where(c => existingTopics.Contains(c.Name)))
                {
                    results[config.Name] = true;
                }

                if (topicsToCreate.Any())
                {
                    // Create new topics
                    var topicSpecs = topicsToCreate.Select(config => new TopicSpecification
                    {
                        Name = config.Name,
                        NumPartitions = config.NumPartitions,
                        ReplicationFactor = config.ReplicationFactor
                    }).ToList();

                    await adminClient.CreateTopicsAsync(topicSpecs, new CreateTopicsOptions
                    {
                        RequestTimeout = timeout ?? TimeSpan.FromSeconds(30)
                    });

                    // Mark all as successful if no exception was thrown
                    foreach (var config in topicsToCreate)
                    {
                        results[config.Name] = true;
                    }
                }

                return results;
            }
            catch (CreateTopicsException ex)
            {
                // Handle partial failures
                foreach (var result in ex.Results ?? Enumerable.Empty<CreateTopicReport>())
                {
                    results[result.Topic] = result.Error.Code == ErrorCode.NoError || 
                                          result.Error.Code == ErrorCode.TopicAlreadyExists;
                }

                // For topics not in the exception results, assume failure
                foreach (var config in configs.Where(c => !results.ContainsKey(c.Name)))
                {
                    results[config.Name] = false;
                }

                return results;
            }
        }

        /// <summary>
        /// Lists all topics in the Kafka cluster
        /// </summary>
        /// <param name="bootstrapServers">Kafka bootstrap servers</param>
        /// <param name="timeout">Timeout for metadata retrieval</param>
        /// <returns>List of topic names</returns>
        public static async Task<List<string>> ListTopicsAsync(
            string bootstrapServers,
            TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(bootstrapServers))
                throw new ArgumentException("Bootstrap servers cannot be null or empty", nameof(bootstrapServers));

            var adminConfig = new AdminClientConfig
            {
                BootstrapServers = bootstrapServers
            };

            using var adminClient = new AdminClientBuilder(adminConfig).Build();

            var metadata = adminClient.GetMetadata(timeout ?? TimeSpan.FromSeconds(30));
            return metadata.Topics
                .Where(t => t.Error.Code == ErrorCode.NoError)
                .Select(t => t.Topic)
                .ToList();
        }
    }
}