# Logstash Configuration - Quick Fix Summary

## ?? What Changed

### Before (Not Working) ?
```ruby
# Logstash couldn't reach Kafka
bootstrap_servers => "kafka:9092"
```

### After (Working) ?
```ruby
# Logstash uses host network to reach Kafka
bootstrap_servers => "host.docker.internal:9092"
```

---

## ?? Summary of All Changes

### 1. ElasticTestContainer.cs - StartLogstashAsync()

#### Added Network Access to Host
```csharp
.WithExtraHost("host.docker.internal", "host-gateway")  // ? NEW
```

#### Increased Initialization Time
```csharp
await Task.Delay(10000);  // ? NEW: 10 seconds for Kafka connection
```

#### Changed Log Level for Debugging
```csharp
.WithEnvironment("LOG_LEVEL", "debug")  // ? Changed from "info"
```

#### Added Connection Verification
```csharp
await VerifyLogstashConnectionsAsync();  // ? NEW method
```

### 2. CreateLogstashPipelineAsync() - Pipeline Configuration

#### Kafka Connection
```ruby
bootstrap_servers => "host.docker.internal:9092"  # ? Changed from "kafka:9092"
```

#### Added Timeout Configuration
```ruby
session_timeout_ms => "30000"         # ? NEW
max_poll_interval_ms => "300000"      # ? NEW
```

### 3. Added GetLogstashLogsAsync() Method

New helper method to retrieve Logstash container logs for debugging:

```csharp
public async Task<string> GetLogstashLogsAsync()
{
    if (_logstashContainer == null)
        return "Logstash container not started";

    var (stdout, stderr) = await _logstashContainer.GetLogsAsync();
    return $"STDOUT:\n{stdout}\n\nSTDERR:\n{stderr}";
}
```

### 4. KafkaLogstashElasticIntegrationTests.cs - Enhanced Diagnostics

#### Added Logstash Log Output
```csharp
var logstashLogs = await _container.GetLogstashLogsAsync();
var logLines = logstashLogs.Split('\n').TakeLast(50);
foreach (var line in logLines)
{
    _output.WriteLine($"   {line}");
}
```

#### Added Index Verification
```csharp
if (searchResponse.Total == 0)
{
    // Check what indices exist
    var catIndices = await _elasticClient.Cat.IndicesAsync(i => i.Index("logs-*"));
    _output.WriteLine("\nExisting indices:");
    foreach (var index in catIndices.Records)
    {
        _output.WriteLine($"   - {index.Index} ({index.DocsCount} docs)");
    }
}
```

#### Increased Wait Time
```csharp
await Task.Delay(15000);  // ? Changed from 10000 (10s ? 15s)
```

---

## ?? Why These Changes Fix The Issue

### Problem 1: Network Isolation
**Issue:** Logstash container couldn't reach Kafka on the host machine.

**Solution:** `host.docker.internal` allows Docker containers to access services running on the host.

### Problem 2: Insufficient Wait Time
**Issue:** Logstash needs time to:
1. Start up
2. Connect to Kafka
3. Subscribe to topics
4. Begin consuming messages
5. Process and index to Elasticsearch

**Solution:** Increased wait times at key points:
- Container initialization: 10 seconds
- Test assertion wait: 15 seconds

### Problem 3: Kafka Consumer Configuration
**Issue:** Default Kafka consumer timeouts too aggressive.

**Solution:** Added explicit timeout configuration:
- `session_timeout_ms: 30000` - More time for heartbeats
- `max_poll_interval_ms: 300000` - More time between polls

### Problem 4: Lack of Diagnostics
**Issue:** Hard to debug when something goes wrong.

**Solution:** Added comprehensive logging:
- Logstash container logs
- Elasticsearch index listing
- Document samples
- Connection verification

---

## ?? How to Use

### Step 1: Ensure Kafka is Running

```bash
# Check Kafka is running
docker ps | grep kafka

# Or start it if using docker-compose
docker-compose -f kafka-docker-compose.yml up -d

# Verify it's accessible
kafka-topics --list --bootstrap-server localhost:9092
```

### Step 2: Create Required Topics

```bash
kafka-topics --create --topic logs-test.error \
  --bootstrap-server localhost:9092 \
  --partitions 3 \
  --replication-factor 1

kafka-topics --create --topic test-logs \
  --bootstrap-server localhost:9092 \
  --partitions 3 \
  --replication-factor 1
```

### Step 3: Run the Test

```bash
dotnet test --filter "KafkaToElasticsearch_ThroughLogstash_ShouldSucceed" \
  --logger "console;verbosity=detailed"
```

### Step 4: Check Output

You should see:
```
? Logstash ready at http://localhost:xxxxx
?? Sending message to Kafka
? Message sent to Kafka: Partition 0, Offset 42
? Waiting for Logstash to process message (15 seconds)...
?? Checking Logstash logs for debugging...
   [INFO] Pipeline started successfully
   [INFO] Kafka input started
?? Searching for message in Elasticsearch...
? Found document in Elasticsearch!
   TraceId: xxx-xxx-xxx
   logstash_processed_at: 2024-01-15T10:30:00Z
```

---

## ?? If Test Still Fails

### Check 1: Logstash Connected to Kafka?

```bash
# Look for this in test output under "Logstash logs":
[INFO] Kafka input plugin started
[INFO] Subscribed to topics: logs-test.error, test-logs

# If you see errors like:
? Connection refused to host.docker.internal:9092
? Broker may not be available
```

**Fix:** Restart Docker Desktop and ensure Kafka is accessible on port 9092.

### Check 2: Message in Kafka?

```bash
kafka-console-consumer --bootstrap-server localhost:9092 \
  --topic logs-test.error \
  --from-beginning \
  --max-messages 1
```

### Check 3: Logstash Consumer Group Active?

```bash
kafka-consumer-groups --bootstrap-server localhost:9092 \
  --group logstash-consumer-group \
  --describe
```

Should show LAG = 0 (caught up).

### Check 4: Documents in Elasticsearch?

```bash
curl http://localhost:9200/logs-*/_count
```

---

## ?? Architecture Flow

```
???????????????????
?  Your Test      ?
?  (C# Code)      ?
???????????????????
         ? Produces JSON
         ?
???????????????????
?  Kafka          ?
?  localhost:9092 ?
???????????????????
         ? host.docker.internal:9092
         ?
???????????????????
?  Logstash       ?
?  (Container)    ?
?  - Consumes     ?
?  - Parses JSON  ?
?  - Enriches     ?
???????????????????
         ? http://elasticsearch:9200
         ?
???????????????????
?  Elasticsearch  ?
?  (Container)    ?
?  Index:         ?
?  logs-YYYY.MM.DD?
???????????????????
```

**Key:** Logstash uses `host.docker.internal:9092` to reach Kafka on host, but `elasticsearch:9200` (Docker network alias) to reach Elasticsearch.

---

## ? Validation Checklist

Before running tests, ensure:

- [x] Kafka running on localhost:9092
- [x] Topics exist (logs-test.error, test-logs)
- [x] Docker Desktop running
- [x] ElasticTestContainer uses host.docker.internal
- [x] Logstash pipeline configured with host.docker.internal:9092
- [x] Test waits 15+ seconds for processing
- [x] Diagnostic logging enabled

---

## ?? Key Takeaways

1. **Docker Networking:** Containers need `host.docker.internal` to reach host services
2. **Wait Times Matter:** Distributed systems need time to process
3. **Diagnostics First:** Always log what's happening at each stage
4. **Test Dependencies:** Ensure all prerequisites (Kafka, topics) exist before testing

---

## ?? Related Documentation

- [LOGSTASH-TROUBLESHOOTING.md](./LOGSTASH-TROUBLESHOOTING.md) - Detailed troubleshooting
- [KAFKA-LOGSTASH-ELASTICSEARCH.md](./KAFKA-LOGSTASH-ELASTICSEARCH.md) - Full architecture
- [SETUP-GUIDE.md](./SETUP-GUIDE.md) - Quick start guide

---

**All changes have been applied! Your test should now work! ??**
