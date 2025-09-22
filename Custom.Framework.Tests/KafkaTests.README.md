# Kafka Integration Tests

This file contains comprehensive integration tests for the Custom.Framework Kafka producer and consumer implementations with different delivery semantics.

## Prerequisites

To run these tests, you need:

### 1. Running Kafka Instance
- Kafka broker running on `localhost:9092` (configurable in test setup)
- Zookeeper instance (required by Kafka)

### 2. Topic Management
The tests now include automatic topic creation functionality. However, if you encounter "Subscribed topic not available" errors, you have several options:

#### Option A: Enable Auto-Topic Creation (Recommended for Testing)
Add these settings to your Kafka broker configuration (`server.properties`):
```properties
auto.create.topics.enable=true
num.partitions=1
default.replication.factor=1
```

#### Option B: Manual Topic Creation
Create topics manually using Kafka CLI:
```bash
# Create a test topic
kafka-topics.sh --create --topic test-topic-example --bootstrap-server localhost:9092 --partitions 1 --replication-factor 1
```

#### Option C: Use Test's Automatic Topic Creation
The tests now include `EnsureTopicExistsAsync()` method that automatically creates topics before each test runs.

### 3. Required NuGet Packages
- Confluent.Kafka (v2.3.0+) - includes AdminClient for topic management
- Serilog - for logging
- xUnit - test framework
- Moq - mocking framework

## Test Coverage

The `KafkaTests.cs` file includes tests for:

### 1. AtMostOnce Delivery Semantics
- **Single Message Test**: Verifies that a message is delivered at most once with no guarantees about delivery
- **Batch Message Test**: Tests batch production with AtMostOnce semantics

### 2. AtLeastOnce Delivery Semantics  
- **Single Message Test**: Verifies that a message is delivered at least once (may have duplicates)
- **Duplicate Detection Test**: Tests that messages include message-id headers when duplicate detection is enabled

### 3. ExactlyOnce Delivery Semantics
- **Single Message Test**: Verifies that a message is delivered exactly once using transactions
- **Multiple Producers Test**: Tests transactional integrity when multiple producers send messages concurrently

### 4. Error Handling
- **Consumer Error Recovery**: Tests that the consumer continues processing after a message handler exception

## Test Structure

Each test follows this pattern:
1. **Arrange**: Set up producer and consumer configurations specific to the delivery semantic being tested
2. **Topic Creation**: Automatically ensure the test topic exists using `EnsureTopicExistsAsync()`
3. **Act**: Send messages using the producer and consume them with the consumer
4. **Assert**: Verify the expected delivery behavior and message integrity

## Key Test Features

- **Automatic Topic Creation**: Tests automatically create required topics before execution
- **Unique Topics**: Each test uses a unique topic to avoid interference
- **Configurable Timeouts**: Tests use appropriate timeouts for different delivery semantics
- **Resource Cleanup**: All producers and consumers are properly disposed after tests
- **Mock Logging**: Uses Serilog mock logger for testing without external dependencies

## Troubleshooting

### "Subscribed topic not available" Error
This error occurs when:
1. The Kafka topic doesn't exist
2. Auto-topic creation is disabled in Kafka configuration
3. The consumer starts before the producer creates the topic

**Solutions:**
1. Enable auto-topic creation in Kafka (easiest for testing)
2. Use the test's automatic topic creation (already implemented)
3. Manually create topics before running tests
4. Check Kafka broker logs for additional error details

### Connection Issues
- Verify Kafka is running on `localhost:9092`
- Check firewall settings
- Ensure Zookeeper is running (required by Kafka)

## Configuration

The tests use these default configurations:

### Producer Configuration
- Bootstrap servers: `localhost:9092`
- Compression: None (for test speed)
- Linger time: 0ms (immediate send)
- Batch size: 16KB
- Message max size: 1MB

### Consumer Configuration  
- Bootstrap servers: `localhost:9092`
- Unique group IDs per test
- Fetch size: 1MB
- Partition fetch size: 512KB

## Running the Tests

```bash
dotnet test Custom.Framework.TestFactory
```

Or run specific test methods:

```bash
dotnet test Custom.Framework.TestFactory --filter "AtMostOnce_ProducerConsumer_ShouldDeliverMessageAtMostOnce"
```

## Test Scenarios Covered

| Test Method | Delivery Semantic | Scenario | Expected Behavior |
|-------------|------------------|----------|-------------------|
| `AtMostOnce_ProducerConsumer_ShouldDeliverMessageAtMostOnce` | AtMostOnce | Single message | Message delivered at most once |
| `AtMostOnce_ProducerBatch_ShouldDeliverAllMessagesAtMostOnce` | AtMostOnce | Batch messages | All messages delivered at most once |
| `AtLeastOnce_ProducerConsumer_ShouldDeliverMessageAtLeastOnce` | AtLeastOnce | Single message | Message delivered at least once |
| `AtLeastOnce_ProducerWithDuplicateDetection_ShouldIncludeMessageId` | AtLeastOnce | Duplicate detection | Message includes message-id header |
| `ExactlyOnce_ProducerConsumer_ShouldDeliverMessageExactlyOnce` | ExactlyOnce | Single message | Message delivered exactly once |
| `ExactlyOnce_MultipleProducers_ShouldMaintainTransactionalIntegrity` | ExactlyOnce | Multiple producers | All messages delivered exactly once |
| `Consumer_HandlerException_ShouldContinueProcessing` | AtLeastOnce | Error handling | Consumer continues after handler error |

## Notes

- Tests are designed to be independent and can run in parallel
- Each test uses unique topics and consumer groups to avoid conflicts
- Tests include appropriate waits and timeouts for async operations
- Error scenarios are tested to ensure robust error handling
- The tests verify both message delivery and the integrity of message content
- Automatic topic creation ensures tests run reliably in different environments