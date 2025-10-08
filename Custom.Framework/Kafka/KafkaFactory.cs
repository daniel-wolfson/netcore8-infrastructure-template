using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Custom.Framework.Kafka
{
    public class KafkaFactory(IOptionsMonitor<KafkaOptions> options, ILogger logger) 
        : IKafkaFactory, IDisposable
    {
        private const int MaxPoolSize = 100;
        private const int CleanupThreshold = 90; // Start cleanup at 90% capacity

        private readonly IOptionsMonitor<KafkaOptions> _options = options;

        // Producer pool with thread-safe access
        private readonly ConcurrentDictionary<string, ProducerPoolEntry> _producerPool = new();
        private readonly SemaphoreSlim _producerPoolLock = new(1, 1);

        // Consumer pool with thread-safe access
        private readonly ConcurrentDictionary<string, IKafkaConsumer> _consumerPool = new();
        private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _consumerGroupPool = new();
        private readonly SemaphoreSlim _consumerGroupPoolLock = new(1, 1);
        private readonly SemaphoreSlim _consumerPoolLock = new(1, 1);


        private bool _disposed;

        public IKafkaConsumer CreateConsumer(string name)
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

                // Create new consumer
                var settings = _options.CurrentValue.Consumers.FirstOrDefault(x => x.Name == name)
                    ?? throw new NullReferenceException($"Consumer not defined for {name}");

                var deliveryStrategy = DeliveryStrategyFactory.CreateConsumerStrategy(
                    settings.DeliverySemantics, settings);

                //var consumer = new KafkaConsumer(groupId, _kafkaOptions.CurrentValue, deliveryStrategy, _logger);

                // Add to pool
                var poolEntry = new KafkaConsumer(name, options.CurrentValue, deliveryStrategy, logger)
                {
                    //Consumer = consumer,
                    CreatedTime = DateTime.UtcNow,
                    LastAccessTime = DateTime.UtcNow,
                    AccessCount = 1
                };

                if (_consumerPool.TryAdd(name, poolEntry))
                {
                    logger.Information("Created and added consumer '{ConsumerName}' to pool. Pool size: {PoolSize}",
                        name, _consumerPool.Count);
                }
                else
                {
                    logger.Warning("Failed to add consumer '{ConsumerName}' to pool", name);
                }

                if (_consumerGroupPool.TryGetValue(poolEntry.GroupId, out var existingGroupEntry))
                {
                    existingGroupEntry.Add(poolEntry.Name);
                    logger.Debug("Added consumer instance '{ConsumerName}' to existing consumer group '{GroupId}'. Group size: {GroupSize}",
                        poolEntry.Name, poolEntry.GroupId, existingGroupEntry.Count);
                }
                else
                {
                    ConcurrentBag<string> newGroupEntry = [ poolEntry.Name ];
                    if (_consumerGroupPool.TryAdd(name, newGroupEntry))
                    {
                        logger.Information("Created and added consumer group '{ConsumerGroupName}' to pool. Pool size: {PoolSize}",
                            name, _consumerGroupPool.Count);
                    }
                    else
                    {
                        logger.Warning("Failed to add consumer group '{ConsumerGroupName}' to pool", name);
                    }
                }

                return poolEntry;
            }
            finally
            {
                _consumerPoolLock.Release();
            }
        }

        public IKafkaProducer CreateProducer(string name)
        {
            // Try to get existing producer from pool
            if (_producerPool.TryGetValue(name, out var poolEntry))
            {
                poolEntry.LastAccessTime = DateTime.UtcNow;
                poolEntry.AccessCount++;
                logger.Debug("Reusing producer '{ProducerName}' from pool. Access count: {AccessCount}",
                    name, poolEntry.AccessCount);
                return poolEntry.Producer;
            }

            // Create new producer if not in pool
            return CreateAndPoolProducer(name);
        }

        private IKafkaProducer CreateAndPoolProducer(string name)
        {
            _producerPoolLock.Wait();
            try
            {
                // Double-check after acquiring lock
                if (_producerPool.TryGetValue(name, out var existingEntry))
                {
                    existingEntry.LastAccessTime = DateTime.UtcNow;
                    existingEntry.AccessCount++;
                    return existingEntry.Producer;
                }

                // Check if pool cleanup is needed
                if (_producerPool.Count >= CleanupThreshold)
                {
                    CleanupProducerPool();
                }

                // Create new producer
                var settings = _options.CurrentValue.Producers.FirstOrDefault(x => x.Name == name)
                    ?? throw new NullReferenceException($"Producer not defined for {name}");

                var deliveryStrategy = DeliveryStrategyFactory.CreateProducerStrategy(
                    settings.DeliverySemantics, settings);

                var producer = new KafkaProducer(name, _options.CurrentValue, deliveryStrategy, logger);

                // Add to pool
                var poolEntry = new ProducerPoolEntry
                {
                    Producer = producer,
                    CreatedTime = DateTime.UtcNow,
                    LastAccessTime = DateTime.UtcNow,
                    AccessCount = 1
                };

                if (_producerPool.TryAdd(name, poolEntry))
                {
                    logger.Information("Created and added producer '{ProducerName}' to pool. Pool size: {PoolSize}",
                        name, _producerPool.Count);
                }
                else
                {
                    logger.Warning("Failed to add producer '{ProducerName}' to pool", name);
                }

                return producer;
            }
            finally
            {
                _producerPoolLock.Release();
            }
        }

        private void CleanupProducerPool()
        {
            logger.Information("Producer pool cleanup initiated. Current size: {PoolSize}", _producerPool.Count);

            // Calculate how many producers to remove
            var targetRemovalCount = _producerPool.Count - (MaxPoolSize / 2); // Remove half when threshold hit
            if (targetRemovalCount <= 0) return;

            // Sort by least recently used (LRU) and lowest access count
            var producersToRemove = _producerPool
                .OrderBy(kvp => kvp.Value.AccessCount)
                .ThenBy(kvp => kvp.Value.LastAccessTime)
                .Take(targetRemovalCount)
                .ToList();

            foreach (var entry in producersToRemove)
            {
                if (_producerPool.TryRemove(entry.Key, out var removedEntry))
                {
                    try
                    {
                        removedEntry.Producer.Dispose();
                        logger.Debug("Removed producer '{ProducerName}' from pool. " +
                            "Access count: {AccessCount}, Age: {Age}",
                            entry.Key,
                            removedEntry.AccessCount,
                            DateTime.UtcNow - removedEntry.CreatedTime);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error disposing producer '{ProducerName}' during cleanup", entry.Key);
                    }
                }
            }

            logger.Information("Producer pool cleanup completed. New size: {PoolSize}", _producerPool.Count);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            logger.Information("Disposing KafkaFactory and cleaning up producer and consumer pools");

            // Dispose all pooled consumers
            foreach (var entry in _consumerPool)
            {
                try
                {
                    entry.Value.UnsubscribeAsync().GetAwaiter().GetResult();
                    logger.Debug("Disposed consumer '{ConsumerName}' from pool", entry.Key);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error disposing consumer '{ConsumerName}'", entry.Key);
                }
            }

            // Dispose all pooled producers
            foreach (var entry in _producerPool)
            {
                try
                {
                    entry.Value.Producer.Dispose();
                    logger.Debug("Disposed producer '{ProducerName}' from pool", entry.Key);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error disposing producer '{ProducerName}'", entry.Key);
                }
            }

            _consumerGroupPool.Clear();
            _producerPool.Clear();
            _consumerGroupPoolLock.Dispose();
            _producerPoolLock.Dispose();
        }

        /// <summary>
        /// Internal class to track producer metadata in the pool
        /// </summary>
        private class ProducerPoolEntry
        {
            public required IKafkaProducer Producer { get; init; }
            public DateTime CreatedTime { get; init; }
            public DateTime LastAccessTime { get; set; }
            public int AccessCount { get; set; }
        }

        /// <summary>
        /// Internal class to track consumer metadata in the pool
        /// </summary>
        private class ConsumerPoolEntry
        {
            public required IKafkaConsumer Consumer { get; init; }
            public DateTime CreatedTime { get; init; }
            public DateTime LastAccessTime { get; set; }
            public int AccessCount { get; set; }
        }
    }
}