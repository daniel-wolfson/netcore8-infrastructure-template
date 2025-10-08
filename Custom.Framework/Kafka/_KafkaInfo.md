# Kafka Information Guide

## Table of Contents
1. [Kafka Fundamentals](#kafka-fundamentals)
2. [Delivery Semantics](#delivery-semantics)
3. [Consumer Groups](#consumer-groups)
4. [Scalability](#scalability)
5. [Best Practices](#best-practices)

---

## Kafka Fundamentals

### What is Apache Kafka?

Apache Kafka is a **distributed streaming platform** designed for building real-time data pipelines and streaming applications. It provides:

- **High throughput**: Handles millions of messages per second
- **Fault tolerance**: Data replication across multiple brokers
- **Durability**: Messages persisted to disk
- **Scalability**: Horizontal scaling by adding more brokers
- **Low latency**: Sub-millisecond message delivery

### Core Concepts

#### 1. **Topics**
A topic is a category or feed name to which records are published.

```
Topic: "orders"
├── Partition 0: [msg1, msg2, msg3, ...]
├── Partition 1: [msg4, msg5, msg6, ...]
└── Partition 2: [msg7, msg8, msg9, ...]
```

- Topics are **multi-subscriber**: Multiple consumers can read from the same topic
- Topics are **partitioned** for scalability and parallelism
- Each partition is an **ordered, immutable sequence** of records

#### 2. **Partitions**
Partitions are the unit of parallelism in Kafka.

```plaintext
Topic: "user-events" (3 partitions)

Partition 0:  [msg-user1] → [msg-user4] → [msg-user7] →
Partition 1:  [msg-user2] → [msg-user5] → [msg-user8] →
Partition 2:  [msg-user3] → [msg-user6] → [msg-user9] →

Key-based routing: user1, user4, user7 → Partition 0
                   user2, user5, user8 → Partition 1
                   user3, user6, user9 → Partition 2
```

**Properties:**
- Messages within a partition are **strictly ordered**
- Each message has an **offset** (unique ID within partition)
- Messages are distributed across partitions by **key** (if provided) or **round-robin**

#### 3. **Brokers**
A Kafka cluster consists of one or more servers called brokers.

```plaintext
Kafka Cluster
├── Broker 1 (Leader for Partition 0, Replica for Partition 1)
├── Broker 2 (Leader for Partition 1, Replica for Partition 2)
└── Broker 3 (Leader for Partition 2, Replica for Partition 0)
```

**Responsibilities:**
- Store data (partitions)
- Serve client requests (produce/consume)
- Replicate data for fault tolerance
- Handle leader election

#### 4. **Producers**
Applications that publish (write) data to topics.

```csharp
// Example: Publishing a message
var producer = _kafkaFactory.CreateProducer("OrderProducer");
await producer.PublishAsync("orders", new Order 
{ 
    Id = "ORD-123", 
    CustomerId = "CUST-456" 
});
```

**Producer Features:**
- **Batching**: Groups messages for efficiency
- **Compression**: Reduces network bandwidth (gzip, snappy, lz4, zstd)
- **Partitioning**: Routes messages to specific partitions
- **Acknowledgments**: Confirms message delivery

#### 5. **Consumers**
Applications that read data from topics.

```csharp
// Example: Consuming messages
var consumer = _kafkaFactory.CreateConsumer("OrderConsumer");
consumer.Subscribe<Order>((order, result, token) =>
{
    Console.WriteLine($"Processing order: {order.Id}");
    return Task.CompletedTask;
});
```

**Consumer Features:**
- **Consumer Groups**: Load balancing across multiple consumers
- **Offset Management**: Tracks position in partition
- **Rebalancing**: Redistributes partitions when consumers join/leave

#### 6. **Offsets**
An offset is a unique identifier for a message within a partition.

```plaintext
Partition 0:
Offset: 0    1    2    3    4    5    6    7
        [m1] [m2] [m3] [m4] [m5] [m6] [m7] [m8]
                      ↑
        Consumer Position (offset 3)
```

**Offset Strategies:**
- **Auto-commit**: Automatically commits offsets periodically
- **Manual commit**: Application explicitly commits after processing
- **Earliest**: Start from beginning of partition
- **Latest**: Start from end of partition (only new messages)

### Message Structure

```csharp
public class KafkaMessage
{
    public string Key { get; set; }        // Routing key (determines partition)
    public string Value { get; set; }      // Message payload
    public Headers Headers { get; set; }   // Metadata (correlation-id, trace-id, etc.)
    public int Partition { get; set; }     // Target partition
    public long Offset { get; set; }       // Unique ID within partition
    public DateTime Timestamp { get; set; } // Message timestamp
}
```

---

## Delivery Semantics

Kafka supports three delivery guarantee levels, each with different trade-offs between performance and reliability.

### 1. At-Most-Once Delivery

**Guarantee**: Messages are delivered **at most once** and may be lost but never duplicated.

```plaintext
Producer                    Broker                    Consumer
   │                          │                          │
   ├─ Send Message ──────────►│                          │
   │  (no ack wait)           │                          │
   │                          ├─ Store Message           │
   │                          │                          │
   │                          │◄─── Fetch ───────────────┤
   │                          ├─ Send Message ──────────►│
   │                          │                          ├─ Process
   │                          │                          ├─ Commit Offset (before processing)
   │                          │                          │
   │   [If failure occurs, message is lost]              │
```

**Configuration:**
```csharp
// Producer
Acks = Acks.None  // Don't wait for broker acknowledgment
EnableIdempotence = false

// Consumer
EnableAutoCommit = true
AutoCommitIntervalMs = 1000  // Commit frequently
```

**Use Cases:**
- Log aggregation (acceptable to lose some logs)
- Metrics collection (approximations acceptable)
- Real-time analytics (recent data more important)

**Pros:** ✅ Highest throughput, lowest latency
**Cons:** ❌ Possible message loss

---

### 2. At-Least-Once Delivery

**Guarantee**: Messages are **never lost** but may be delivered multiple times (duplicates possible).

```plaintext
Producer                    Broker                    Consumer
   │                          │                          │
   ├─ Send Message ──────────►│                          │
   │                          ├─ Store Message           │
   │◄─── Ack ─────────────────┤                          │
   │                          │                          │
   │                          │◄─── Fetch ───────────────┤
   │                          ├─ Send Message ──────────►│
   │                          │                          ├─ Process
   │                          │                          ├─ Commit Offset (after processing)
   │                          │                          │
   │   [If failure before commit, message redelivered]   │
```

**Configuration:**
```csharp
// Producer
Acks = Acks.All  // Wait for all replicas to acknowledge
EnableIdempotence = false
MaxInFlight = 5  // Allow retries

// Consumer
EnableAutoCommit = false  // Manual commit
// Commit after successful processing
consumer.Commit(result);
```

**Idempotency Pattern:**
```csharp
var processedMessages = new HashSet<string>();

consumer.Subscribe<Order>((order, result, token) =>
{
    var messageId = result.Message.Headers
        .FirstOrDefault(h => h.Key == "message-id")
        .GetValueString();
    
    // Check for duplicate
    if (processedMessages.Contains(messageId))
    {
        Console.WriteLine($"Skipping duplicate: {messageId}");
        return Task.CompletedTask;
    }
    
    // Process message
    ProcessOrder(order);
    
    // Track processed message
    processedMessages.Add(messageId);
    
    return Task.CompletedTask;
});
```

**Use Cases:**
- Order processing (duplicates handled by idempotency)
- Payment transactions (deduplication required)
- Event sourcing
- Financial systems

**Pros:** ✅ No message loss, good throughput
**Cons:** ❌ Possible duplicates (requires deduplication)

---

### 3. Exactly-Once Delivery

**Guarantee**: Messages are delivered **exactly once** - no loss, no duplicates.

```plaintext
Producer                    Broker                    Consumer
   │                          │                          │
   ├─ Begin Transaction ─────►│                          │
   ├─ Send Message ──────────►│                          │
   │                          ├─ Store (uncommitted)     │
   ├─ Commit Transaction ────►│                          │
   │                          ├─ Mark Committed          │
   │◄──── Ack ────────────────┤                          │
   │                          │                          │
   │                          │◄─── Fetch ───────────────┤
   │                          ├─ Send Message ──────────►│
   │                          │                          ├─ Begin Transaction
   │                          │                          ├─ Process
   │                          │◄─ Commit Offset ─────────┤
   │                          │                          ├─ Commit Transaction
   │                          │                          │
   │   [Atomic: process + commit]                        │
```

**Configuration:**
```csharp
// Producer
EnableIdempotence = true
TransactionalId = "unique-producer-id"
Acks = Acks.All
MaxInFlight = 5

// Consumer
IsolationLevel = IsolationLevel.ReadCommitted  // Only read committed messages
EnableAutoCommit = false
```

**Implementation:**
```csharp
// Producer with transactions
var producer = _kafkaFactory.CreateProducer("TransactionalProducer");
producer.InitTransactions();

try
{
    producer.BeginTransaction();
    
    await producer.PublishAsync("orders", order1);
    await producer.PublishAsync("inventory", inventoryUpdate);
    
    producer.CommitTransaction();
}
catch (Exception ex)
{
    producer.AbortTransaction();
    throw;
}
```

**Use Cases:**
- Banking/financial transactions
- Distributed transactions (exactly-once processing)
- Critical business operations
- Cross-topic writes (all-or-nothing)

**Pros:** ✅ Strongest guarantee, no duplicates, no loss
**Cons:** ❌ Lower throughput, higher latency, more complex

---

### Comparison Table

| Feature             | At-Most-Once      | At-Least-Once    | Exactly-Once |
|---------------------|-------------------|------------------|--------------|
| **Message Loss**    | Possible          | Never            | Never        |
| **Duplicates**      | Never             | Possible         | Never        |
| **Throughput**      | Highest           | High             | Medium       |
| **Latency**         | Lowest            | Low              | Medium       |
| **Complexity**      | Low               | Medium           | High         |
| **Producer Acks**   | None              | All              | All + Transactions |
| **Consumer Commit** | Before processing | After processing | Transactional|
| **Use Case**        | Logs, Metrics     | Most applications| Financial, Critical |

---

## Consumer Groups

### What is a Consumer Group?

A **consumer group** is a set of consumers that cooperatively consume messages from a topic. Each partition is consumed by **exactly one consumer** within a group.

```plaintext
Topic: "orders" (4 partitions)

┌─────────────────────────────────────────────────┐
│             Consumer Group "order-processors"   │
│                                                 │
│  Consumer 1 ──► Partition 0                     │
│  Consumer 1 ──► Partition 1                     │
│  Consumer 2 ──► Partition 2                     │
│  Consumer 3 ──► Partition 3                     │
└─────────────────────────────────────────────────┘

Load balanced: Each consumer handles 1-2 partitions
```

### Key Properties

#### 1. **Group ID**
Identifies the consumer group.

```csharp
public class ConsumerSettings : ConsumerConfig
{
    public string GroupId { get; set; } = "my-consumer-group";
}
```

**Important:**
- Consumers with the **same GroupId** share partitions
- Consumers with **different GroupIds** receive all messages independently

#### 2. **Partition Assignment**
Kafka automatically assigns partitions to consumers.

```plaintext
Scenario 1: More partitions than consumers
Topic: 6 partitions, Consumer Group: 2 consumers

Consumer 1: Partitions [0, 1, 2]
Consumer 2: Partitions [3, 4, 5]
✅ Load balanced


Scenario 2: Equal partitions and consumers
Topic: 4 partitions, Consumer Group: 4 consumers

Consumer 1: Partition [0]
Consumer 2: Partition [1]
Consumer 3: Partition [2]
Consumer 4: Partition [3]
✅ Optimal parallelism


Scenario 3: More consumers than partitions
Topic: 3 partitions, Consumer Group: 5 consumers

Consumer 1: Partition [0]
Consumer 2: Partition [1]
Consumer 3: Partition [2]
Consumer 4: [IDLE]
Consumer 5: [IDLE]
⚠️ Wasted resources
```

#### 3. **Rebalancing**
When consumers join or leave, Kafka **rebalances** partition assignments.

```plaintext
Initial State (3 consumers, 6 partitions):
Consumer A: [0, 1]
Consumer B: [2, 3]
Consumer C: [4, 5]

Consumer C crashes ❌

Rebalancing...

New State (2 consumers):
Consumer A: [0, 1, 2]
Consumer B: [3, 4, 5]
✅ Partitions redistributed
```

**Rebalance Triggers:**
- Consumer joins the group
- Consumer leaves/crashes
- New partitions added to topic
- Consumer heartbeat timeout

**Rebalance Strategies:**
- **Range**: Assigns contiguous ranges to consumers
- **RoundRobin**: Distributes partitions evenly
- **Sticky**: Minimizes partition movement during rebalance
- **CooperativeSticky**: Rebalances incrementally (Kafka 2.4+)

### Multiple Consumer Groups

Different groups consume the **same messages independently**.

```plaintext
Topic: "user-events"

┌──────────────────────────────────────┐
│  Consumer Group "analytics"          │
│    Consumer A ──► All partitions     │
│    (Aggregates statistics)           │
└──────────────────────────────────────┘
        ↓ (reads same messages)
┌──────────────────────────────────────┐
│  Consumer Group "notifications"      │
│    Consumer B ──► All partitions     │
│    (Sends email notifications)       │
└──────────────────────────────────────┘
        ↓ (reads same messages)
┌──────────────────────────────────────┐
│  Consumer Group "fraud-detection"    │
│    Consumer C ──► All partitions     │
│    (Detects suspicious activity)     │
└──────────────────────────────────────┘
```

### Consumer Group Configuration

```csharp
public class ConsumerSettings
{
    // Group identification
    public string GroupId { get; set; } = "my-service-consumers";
    
    // Partition assignment strategy
    public PartitionAssignmentStrategy PartitionAssignmentStrategy { get; set; } 
        = PartitionAssignmentStrategy.CooperativeSticky;
    
    // Session timeout (heartbeat)
    public int SessionTimeoutMs { get; set; } = 45000; // 45 seconds
    
    // Heartbeat interval
    public int HeartbeatIntervalMs { get; set; } = 3000; // 3 seconds
    
    // Max poll interval (time between polls)
    public int MaxPollIntervalMs { get; set; } = 300000; // 5 minutes
    
    // Rebalance timeout
    public int RebalanceTimeoutMs { get; set; } = 60000; // 1 minute
}
```

### Consumer Group Coordination

```plaintext
┌──────────────────────────────────────────────┐
│         Kafka Cluster                        │
│                                              │
│  ┌────────────────────────────────────┐     │
│  │   Group Coordinator (Broker)       │     │
│  │   - Manages group membership       │     │
│  │   - Triggers rebalances            │     │
│  │   - Tracks consumer heartbeats     │     │
│  │   - Stores offset commits          │     │
│  └────────────────────────────────────┘     │
│             ↕️ Heartbeats                    │
│  ┌─────────────┬─────────────┬─────────┐    │
│  │ Consumer 1  │ Consumer 2  │Consumer 3│    │
│  └─────────────┴─────────────┴─────────┘    │
└──────────────────────────────────────────────┘
```

### Offset Management in Groups

Each consumer group maintains its **own offsets**.

```plaintext
Topic "orders" - Partition 0:
Offset: [0] [1] [2] [3] [4] [5] [6] [7] [8] [9]

Consumer Group "processing":     Offset: 5
Consumer Group "analytics":      Offset: 8
Consumer Group "audit":          Offset: 3

✅ Each group progresses independently
```

**Offset Storage:**
- Stored in special Kafka topic: `__consumer_offsets`
- Replicated for fault tolerance
- Committed automatically or manually

---

## Scalability

### Horizontal Scaling

Kafka scales horizontally by adding more resources.

#### 1. **Scaling Producers**

```plaintext
Before:
Single Producer → Topic (3 partitions)
Throughput: 10K msg/sec

After:
Producer 1 ──┐
Producer 2 ──┼──► Topic (3 partitions)
Producer 3 ──┘
Throughput: 30K msg/sec

✅ 3x throughput increase
```

**Implementation:**
```csharp
// Create multiple producer instances
var producer1 = _kafkaFactory.CreateProducer("Producer1");
var producer2 = _kafkaFactory.CreateProducer("Producer2");
var producer3 = _kafkaFactory.CreateProducer("Producer3");

// Distribute messages across producers
await Task.WhenAll(
    producer1.PublishAsync("orders", batch1),
    producer2.PublishAsync("orders", batch2),
    producer3.PublishAsync("orders", batch3)
);
```

#### 2. **Scaling Consumers**

```plaintext
Scenario A: Scale up (add consumers)
Topic: 6 partitions

Before:
Consumer Group (1 consumer)
└── Consumer 1: [P0, P1, P2, P3, P4, P5]
    Throughput: 10K msg/sec

After:
Consumer Group (3 consumers)
├── Consumer 1: [P0, P1]
├── Consumer 2: [P2, P3]
└── Consumer 3: [P4, P5]
    Throughput: 30K msg/sec

✅ 3x throughput increase
```

**Optimal Ratio:**
```
Number of Consumers ≤ Number of Partitions
```

**Example:**
- 12 partitions → 12 consumers (max parallelism)
- 12 partitions → 6 consumers (good balance)
- 12 partitions → 3 consumers (underutilized)
- 12 partitions → 24 consumers (16 idle consumers, wasted resources)

#### 3. **Scaling Brokers**

```plaintext
Before (3 brokers):
Topic "orders" (6 partitions, replication factor 2)

Broker 1: [P0-Leader, P1-Follower, P2-Leader]
Broker 2: [P1-Leader, P3-Leader, P4-Follower]
Broker 3: [P2-Follower, P4-Leader, P5-Leader]

After (5 brokers):
Broker 1: [P0-Leader, P1-Follower]
Broker 2: [P1-Leader, P2-Follower]
Broker 3: [P2-Leader, P3-Follower]
Broker 4: [P3-Leader, P4-Follower]
Broker 5: [P4-Leader, P5-Leader]

✅ Better load distribution
```

#### 4. **Scaling Partitions**

```plaintext
Before:
Topic "orders" (3 partitions)
Consumer Group (3 consumers)
├── Consumer 1: [P0] → 10K msg/sec
├── Consumer 2: [P1] → 10K msg/sec
└── Consumer 3: [P2] → 10K msg/sec
Total: 30K msg/sec

After (increase partitions):
Topic "orders" (6 partitions)
Consumer Group (6 consumers)
├── Consumer 1: [P0] → 10K msg/sec
├── Consumer 2: [P1] → 10K msg/sec
├── Consumer 3: [P2] → 10K msg/sec
├── Consumer 4: [P3] → 10K msg/sec
├── Consumer 5: [P4] → 10K msg/sec
└── Consumer 6: [P5] → 10K msg/sec
Total: 60K msg/sec

✅ 2x throughput increase
```

**⚠️ Warning:** You can only **add** partitions, not remove them (without recreating the topic).

### Performance Optimization

#### 1. **Batching**

```csharp
// Producer batching
var producerConfig = new ProducerConfig
{
    LingerMs = 10,              // Wait up to 10ms to batch messages
    BatchSize = 16384,          // 16KB batch size
    CompressionType = "snappy"  // Compress batches
};

// Results:
// - 10 individual sends: 10ms latency each
// - 1 batched send (10 messages): 10ms latency total
```

#### 2. **Compression**

```plaintext
Compression Types:
┌─────────┬───────────┬──────────┬────────────┐
│  Type   │   Ratio   │ CPU Cost │  Use Case  │
├─────────┼───────────┼──────────┼────────────┤
│ none    │    1x     │   Low    │ Local      │
│ gzip    │    4x     │   High   │ WAN        │
│ snappy  │    2.5x   │   Low    │ Balanced   │
│ lz4     │    2x     │   Low    │ Fast       │
│ zstd    │    3.5x   │   Medium │ Best ratio │
└─────────┴───────────┴──────────┴────────────┘
```

#### 3. **Parallelism**

```csharp
// Framework's built-in concurrency
public class KafkaConsumer
{
    private readonly int _maxConcurrency = Environment.ProcessorCount / 2;
    
    // Multiple processing tasks
    _processingTasks = new Task[_maxConcurrency];
    for (int i = 0; i < _maxConcurrency; i++)
    {
        _processingTasks[i] = Task.Run(async () =>
        {
            await ProcessFromChannelAsync(i, messageHandler, _cancelToken);
        });
    }
}
```

#### 4. **Connection Pooling**

```csharp
// Use factory pooling (implemented in KafkaFactory)
// Reuse connections instead of creating new ones
var producer = _kafkaFactory.CreateProducer("OrderProducer");  // From pool
await producer.PublishAsync("orders", message);
// Producer stays in pool for reuse
```

### Scalability Patterns

#### Pattern 1: Fan-Out

```plaintext
Single Producer → Multiple Consumer Groups

Producer ──► Topic "user-events"
                    │
        ┌───────────┼───────────┬───────────┐
        ↓           ↓           ↓           ↓
    Group A     Group B     Group C     Group D
    (Email)     (SMS)       (Push)      (Analytics)

✅ Each group processes all messages independently
```

#### Pattern 2: Load Balancing

```plaintext
Multiple Producers → Consumer Group → Multiple Consumers

Producer 1 ──┐
Producer 2 ──┼──► Topic (6 partitions)
Producer 3 ──┘           │
                         │
        ┌────────────────┼────────────────┐
        ↓                ↓                ↓
   Consumer 1      Consumer 2      Consumer 3
   [P0, P1]        [P2, P3]        [P4, P5]

✅ Balanced load across consumers
```

#### Pattern 3: Hierarchical Topics

```plaintext
Fine-grained topics for better parallelism

Topic "events"
├── events.user.login
├── events.user.logout
├── events.order.created
├── events.order.fulfilled
└── events.payment.completed

Consumer Group "user-events"
├── Consumer 1 → events.user.*

Consumer Group "order-events"
├── Consumer 2 → events.order.*
└── Consumer 3 → events.order.*

✅ Topic-level separation + consumer parallelism
```

### Capacity Planning

#### Formula: Messages per Second

```
Total Throughput = Partitions × (Consumer Throughput per Partition)

Example:
- Topic: 12 partitions
- Consumer: 1,000 msg/sec per partition
- Total: 12 × 1,000 = 12,000 msg/sec
```

#### Formula: Consumer Count

```
Optimal Consumers = min(Partitions, Target Throughput / Consumer Capacity)

Example:
- Partitions: 20
- Target: 50,000 msg/sec
- Consumer capacity: 5,000 msg/sec
- Optimal: min(20, 50,000 / 5,000) = min(20, 10) = 10 consumers
```

#### Formula: Partition Count

```
Partitions = max(
    Target Throughput / Producer Throughput,
    Target Throughput / Consumer Throughput
)

Example:
- Target: 100,000 msg/sec
- Producer: 10,000 msg/sec
- Consumer: 5,000 msg/sec
- Partitions: max(100,000/10,000, 100,000/5,000) = max(10, 20) = 20
```

### Monitoring Metrics

```plaintext
Key Metrics to Monitor:

Producer:
├── request-latency-avg
├── record-send-rate
├── compression-rate
└── buffer-available-bytes

Consumer:
├── records-consumed-rate
├── fetch-latency-avg
├── records-lag-max
└── commit-latency-avg

Broker:
├── bytes-in-per-sec
├── bytes-out-per-sec
├── under-replicated-partitions
└── active-controller-count
```

---

## Best Practices

### 1. **Partition Strategy**

```csharp
// ✅ Good: Use meaningful keys for ordering
await producer.PublishAsync("orders", new Order
{
    Key = $"customer-{customerId}",  // All orders for same customer → same partition
    Value = orderData
});

// ❌ Bad: Random keys lose ordering
await producer.PublishAsync("orders", new Order
{
    Key = Guid.NewGuid().ToString(),  // Random partition assignment
    Value = orderData
});
```

### 2. **Error Handling**

```csharp
// ✅ Good: Implement retry with exponential backoff
consumer.Subscribe<Order>((order, result, token) =>
{
    try
    {
        ProcessOrder(order);
    }
    catch (TransientException ex)
    {
        // Retry with backoff
        await Task.Delay(CalculateBackoff(retryCount));
        // Framework handles this automatically
    }
    catch (PermanentException ex)
    {
        // Send to DLQ
        await dlqProducer.PublishAsync("orders-dlq", order);
    }
    return Task.CompletedTask;
});
```

### 3. **Idempotency**

```csharp
// ✅ Good: Check for duplicates
var processedIds = new HashSet<string>();

consumer.Subscribe<Order>((order, result, token) =>
{
    var messageId = GetMessageId(result.Message.Headers);
    
    if (processedIds.Contains(messageId))
    {
        _logger.Information("Duplicate detected: {MessageId}", messageId);
        return Task.CompletedTask;  // Skip
    }
    
    ProcessOrder(order);
    processedIds.Add(messageId);
    
    return Task.CompletedTask;
});
```

### 4. **Resource Management**

```csharp
// ✅ Good: Use factory pooling
public class OrderService
{
    private readonly IKafkaFactory _factory;
    
    public async Task PublishOrder(Order order)
    {
        var producer = _factory.CreateProducer("OrderProducer");  // From pool
        await producer.PublishAsync("orders", order);
        // Producer returns to pool automatically
    }
}

// ❌ Bad: Create new instances
public class OrderService
{
    public async Task PublishOrder(Order order)
    {
        var producer = new KafkaProducer(...);  // Expensive
        await producer.PublishAsync("orders", order);
        producer.Dispose();  // Wasteful
    }
}
```

### 5. **Monitoring**

```csharp
// ✅ Good: Track metrics
consumer.Subscribe<Order>((order, result, token) =>
{
    var sw = Stopwatch.StartNew();
    
    try
    {
        ProcessOrder(order);
        _metrics.RecordSuccess(sw.Elapsed);
    }
    catch (Exception ex)
    {
        _metrics.RecordFailure(sw.Elapsed);
        throw;
    }
    
    return Task.CompletedTask;
});
```

---

## Summary

### Quick Reference

| Concept | Key Takeaway |
|---------|-------------|
| **Topics** | Logical channels for messages |
| **Partitions** | Unit of parallelism, ordered within partition |
| **Consumer Groups** | Load balancing, each partition → one consumer |
| **At-Most-Once** | Fast but may lose messages |
| **At-Least-Once** | No loss but possible duplicates |
| **Exactly-Once** | No loss, no duplicates, but slower |
| **Scalability** | Add partitions, consumers, or brokers |

### Framework Configuration

```json
{
  "Kafka": {
    "Common": {
      "BootstrapServers": "localhost:9092",
      "ServiceShortName": "my-service"
    },
    "Producers": [
      {
        "Name": "OrderProducer",
        "Topics": ["orders"],
        "DeliverySemantics": "AtLeastOnce",
        "EnableIdempotence": true
      }
    ],
    "Consumers": [
      {
        "Name": "OrderConsumer",
        "GroupId": "order-processing-group",
        "Topics": ["orders"],
        "DeliverySemantics": "AtLeastOnce",
        "ChannelCapacity": 1000
      }
    ]
  }
}
```

## C# Custom optimizations using channel, explainations:
### 1. Producer-Consumer Pattern with Channels
•	Before: Single-threaded message processing
•	After: Separate consume and process threads with bounded channel buffering
•	Benefit: Higher throughput, better resource utilization
### 2. Concurrent Message Processing
•	Before: Sequential message handling
•	After: Multiple worker threads processing messages concurrently
•	Benefit: Parallel processing without blocking consumption
### 3. Optimized Consumer Configuration
•	Added FetchMinBytes and FetchWaitMaxMs for batch efficiency
•	Tuned session timeouts and heartbeat intervals
•	Reduced metadata refresh overhead
### 4. Memory and Allocation Optimizations
•	Static delegates to avoid closure allocations
•	Span<T> for string operations
•	Reused exception instances
•	Proper ConfigureAwait(false) usage
### 5. Better Error Handling
•	Separated retryable vs non-retryable exceptions
•	Exponential backoff with jitter and caps
•	Proper cancellation handling without exception allocation
### 6. Improved Resource Management
•	Semaphore-based concurrency control
•	Bounded channel with backpressure handling
•	Graceful shutdown with timeout handling
### Trade-offs:
1.	Complexity: More complex code structure
2.	Memory: Slightly higher memory usage for channels and semaphores
3.	Ordering: Messages may be processed out of order within a partition
### Performance Benefits:
•	Throughput: 3-5x improvement for I/O-bound handlers
•	Latency: Reduced head-of-line blocking
•	Scalability: Better CPU utilization
•	Memory: Reduced allocations in hot path

 ---

## Additional Resources

- [Apache Kafka Documentation](https://kafka.apache.org/documentation/)
- [Confluent Kafka .NET Client](https://github.com/confluentinc/confluent-kafka-dotnet)
- [Kafka: The Definitive Guide](https://www.confluent.io/resources/kafka-the-definitive-guide/)

---
**Version**: 1.0  
**Last Updated**: 2024-01-15  
**Framework Version**: Custom.Framework v1.2.0
