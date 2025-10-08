using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Custom.Framework.Kafka
{
    public class KafkaConsumerGroup : IKafkaConsumerGroup, IDisposable
    {
        private const int MaxPoolSize = 100;
        private const int CleanupThreshold = 90; // Start cleanup at 90% capacity

        private readonly ConcurrentDictionary<string, IKafkaConsumer> _consumerPool = new();
        private readonly SemaphoreSlim _consumerPoolLock = new(1, 1);

        private readonly IKafkaFactory _kafkaFactory;
        private readonly ILogger _logger;
        private readonly KafkaOptions _kafkaOptions;

        public DateTime CreatedTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public int AccessCount { get; set; }

        IEnumerable<IKafkaConsumer> IKafkaConsumerGroup.Consumers => _consumerPool.Values;

        public KafkaConsumerGroup(IKafkaFactory kafkaFactory,
            IOptionsMonitor<KafkaOptions> kafkaOptionsMonitor, ILogger logger)
        {
            _kafkaOptions = kafkaOptionsMonitor.CurrentValue;
            _kafkaFactory = kafkaFactory;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves an existing Kafka consumer with the specified name from the pool, 
        /// or creates and adds a new one if none exists. it depend of settings in KafkaOptions
        /// </summary>
        public IKafkaConsumer GetOrAdd(string consumerName)
        {
            var groupId = _kafkaOptions.Consumers.FirstOrDefault(x => x.Name == consumerName)?.GroupId
                ?? throw new NullReferenceException($"Consumer not defined for {consumerName}");
            var consumer = _consumerPool.GetOrAdd(consumerName, x => CreateConsumer(consumerName));
            return consumer;
        }
        public IKafkaConsumer GetOrAdd(IKafkaConsumer existConsumer)
        {
            var groupId = _kafkaOptions.Consumers.FirstOrDefault(x => x.Name == existConsumer.Name)?.GroupId
                ?? throw new NullReferenceException($"Consumer not defined for {existConsumer.Name}");
            var consumer = _consumerPool.AddOrUpdate(
                existConsumer.Name,
                existConsumer,
                (key, oldValue) => existConsumer
            );
            return consumer;
        }

        public async void RemoveFromGroup(string consumerName)
        {
            if (_consumerPool.TryRemove(consumerName, out var consumer))
            {
                await consumer.UnsubscribeAsync();
                consumer.Dispose();
            }
        }

        private IKafkaConsumer CreateConsumer(string name)
        {
            _consumerPoolLock.Wait();
            try
            {
                // Double-check after acquiring lock
                if (_consumerPool.TryGetValue(name, out var existingEntry))
                {
                    existingEntry.LastAccessTime = DateTime.UtcNow;
                    existingEntry.AccessCount++;
                    return existingEntry;
                }

                // Check if pool cleanup is needed
                if (_consumerPool.Count >= CleanupThreshold)
                {
                    CleanupConsumerPool();
                }

                // Create new consumer
                var settings = _kafkaOptions.Consumers.FirstOrDefault(x => x.Name == name)
                    ?? throw new NullReferenceException($"Consumer not defined for {name}");

                var deliveryStrategy = DeliveryStrategyFactory.CreateConsumerStrategy(
                    settings.DeliverySemantics, settings);

                //var consumer = new KafkaConsumer(name, _kafkaOptions.CurrentValue, deliveryStrategy, _logger);

                // Add to pool
                var poolEntry = new KafkaConsumer(settings.Name, _kafkaOptions, deliveryStrategy, _logger)
                {
                    //Consumer = consumer,
                    CreatedTime = DateTime.UtcNow,
                    LastAccessTime = DateTime.UtcNow,
                    AccessCount = 1
                };

                if (_consumerPool.TryAdd(name, poolEntry))
                {
                    _logger.Information("Created and added consumer '{ConsumerName}' to pool. Pool size: {PoolSize}",
                        name, _consumerPool.Count);
                }
                else
                {
                    _logger.Warning("Failed to add consumer '{ConsumerName}' to pool", name);
                }

                return poolEntry;
            }
            finally
            {
                _consumerPoolLock.Release();
            }
        }

        private void CleanupConsumerPool()
        {
            _logger.Information("Consumer pool cleanup initiated. Current size: {PoolSize}", _consumerPool.Count);

            // Calculate how many consumers to remove
            var targetRemovalCount = _consumerPool.Count - (MaxPoolSize / 2); // Remove half when threshold hit
            if (targetRemovalCount <= 0) return;

            // Sort by least recently used (LRU) and lowest access count
            var consumersToRemove = _consumerPool
                .OrderBy(kvp => kvp.Value.AccessCount)
                .ThenBy(kvp => kvp.Value.LastAccessTime)
                .Take(targetRemovalCount)
                .ToList();

            foreach (var entry in consumersToRemove)
            {
                if (_consumerPool.TryRemove(entry.Key, out var removedEntry))
                {
                    try
                    {
                        // Unsubscribe before disposing
                        removedEntry.UnsubscribeAsync().GetAwaiter().GetResult();

                        _logger.Debug("Removed consumer '{ConsumerName}' from pool. " +
                            "Access count: {AccessCount}, Age: {Age}",
                            entry.Key,
                            removedEntry.AccessCount,
                            DateTime.UtcNow - removedEntry.CreatedTime);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error disposing consumer '{ConsumerName}' during cleanup", entry.Key);
                    }
                }
            }

            _logger.Information("Consumer pool cleanup completed. New size: {PoolSize}", _consumerPool.Count);
        }

        public void Dispose()
        {
            //_kafkaFactory.Dispose(); TODO: implement
        }
    }
}
