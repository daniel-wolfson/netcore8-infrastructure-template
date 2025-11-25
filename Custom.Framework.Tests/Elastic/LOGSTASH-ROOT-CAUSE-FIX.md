# Logstash Not Sending to Elasticsearch - Root Cause Analysis

## ?? Problem Identified

Based on your logs and test failure (`searchResponse.Total == 0`), the issue is:

### **Root Cause: Incorrect Logstash Pipeline Configuration**

#### Issue 1: Conflicting JSON Parsing
```ruby
# ? WRONG Configuration:
input {
  kafka {
    codec => json  # <-- Message is already parsed here
  }
}

filter {
  if [message] {  # <-- This field doesn't exist!
    json {
      source => "message"  # <-- Trying to parse non-existent field
    }
  }
}
```

**Problem**: When you use `codec => json` in Kafka input, the message is **already parsed**. The filter tries to find a `message` field and parse it again, but since it doesn't exist, nothing happens.

#### Issue 2: `auto_offset_reset => "latest"`
```ruby
# ? WRONG for testing:
auto_offset_reset => "latest"  # Only consumes NEW messages
```

**Problem**: If Logstash subscribes to Kafka **AFTER** you send the test message, it will never consume it because `latest` means "only new messages from now on".

## ? Solution Applied

### Fix 1: Removed Redundant JSON Parsing
```ruby
# ? CORRECT Configuration:
input {
  kafka {
    codec => "json"  # Message parsed here
  }
}

filter {
  # No need to parse 'message' field
  # Fields are already at root level
  
  # Just add metadata
  mutate {
    add_field => { 
      "logstash_processed_at" => "%{@timestamp}"
      "pipeline" => "kafka-to-elasticsearch"
    }
  }
}
```

### Fix 2: Changed to `earliest`
```ruby
# ? CORRECT for testing:
auto_offset_reset => "earliest"  # Consumes from beginning
```

### Fix 3: Increased Wait Times
- Logstash initialization: 10s ? 20s (with additional 10s for Kafka connection)
- Test wait time: 15s ? 20s

## ?? How Data Flows

### Current (Correct) Flow:
```
Your Test
    ?
    ? 1. Sends JSON string: {"TraceId":"xxx","Level":"Error",...}
    ?
Kafka Topic
    ?
    ? 2. Logstash Kafka input with codec => "json"
    ?
Logstash (parsed automatically)
    ? Event fields:
    ? - TraceId: "xxx"
    ? - Level: "Error"
    ? - Message: "Test error message..."
    ? - Timestamp: "2024-01-15T..."
    ?
    ? 3. Filter adds metadata
    ?
Logstash (after filter)
    ? Event fields:
    ? - TraceId: "xxx"
    ? - Level: "Error"
    ? - Message: "Test error message..."
    ? - logstash_processed_at: "2024-01-15T16:45:00Z"
    ? - pipeline: "kafka-to-elasticsearch"
    ?
    ? 4. Elasticsearch output
    ?
Elasticsearch
    ? Index: logs-2024.01.15
    ? Document ID: xxx (TraceId)
    ??? Document stored! ?
```

### Previous (Broken) Flow:
```
Your Test
    ?
    ? 1. Sends JSON string
    ?
Kafka Topic
    ?
    ? 2. Logstash Kafka input with codec => "json"
    ?
Logstash (parsed automatically)
    ? Event fields: TraceId, Level, Message, etc.
    ? NOTE: NO 'message' field!
    ?
    ? 3. Filter checks: if [message] { ... }
    ?    Condition FALSE! (field doesn't exist)
    ?    ? JSON filter SKIPPED
    ?    ? No fields extracted
    ?
Logstash (after filter - data lost!)
    ? Event is incomplete or empty
    ?
    ? 4. Elasticsearch output
    ?
Elasticsearch
    ? ? Document not indexed OR
    ??? ? Document missing fields
```

## ??? What Changed in Code

### ElasticTestContainer.cs - CreateLogstashPipelineAsync()

**Before:**
```ruby
input {
  kafka {
    codec => json
    auto_offset_reset => "latest"
  }
}

filter {
  if [message] {
    json { source => "message" }
    ruby { ... }  # Complex parsing
  }
}
```

**After:**
```ruby
input {
  kafka {
    codec => "json"  # Parse once
    auto_offset_reset => "earliest"  # Get all messages
  }
}

filter {
  # No JSON parsing needed!
  # Just add metadata
  mutate {
    add_field => { 
      "logstash_processed_at" => "%{@timestamp}"
    }
  }
}
```

### KafkaLogstashElasticIntegrationTests.cs

**Changed wait time:**
```csharp
// Before:
await Task.Delay(15000);  // 15 seconds

// After:
await Task.Delay(20000);  // 20 seconds
```

**Added note explaining timing:**
```csharp
// Need time for:
// - Kafka subscription (5s)
// - Consumption (3s)
// - Processing (2s)
// - ES indexing (5s)
// = 15s + 5s buffer = 20s
```

## ?? How to Verify the Fix

### Step 1: Check Logstash Logs

Look for these indicators in the test output:

? **Good Signs:**
```
[INFO] Pipeline started successfully
[INFO] Starting kafka input
[INFO] Subscribed to topics: logs-test.error, test-logs
```

? **Bad Signs:**
```
[ERROR] Failed to parse JSON
[ERROR] Connection refused to host.docker.internal:9092
[WARN] Broker may not be available
```

### Step 2: Check Elasticsearch Directly

After the test runs, query Elasticsearch:

```bash
# Check if index was created
curl http://localhost:9200/_cat/indices?v | grep logs-

# Search for your message
curl http://localhost:9200/logs-*/_search?pretty -d '
{
  "query": {
    "match_all": {}
  },
  "size": 10
}'
```

### Step 3: Check Kafka Consumer Group

Verify Logstash is consuming:

```bash
kafka-consumer-groups --bootstrap-server localhost:9092 \
  --group logstash-consumer-group \
  --describe
```

Expected output:
```
GROUP                    TOPIC            PARTITION  CURRENT-OFFSET  LAG
logstash-consumer-group  logs-test.error  0          1               0
```

**LAG = 0** means Logstash is caught up! ?

## ?? Testing Checklist

Before running the test, ensure:

1. ? Kafka is running: `kafka-topics --list --bootstrap-server localhost:9092`
2. ? Topics exist:
   ```bash
   kafka-topics --create --topic logs-test.error --bootstrap-server localhost:9092
   kafka-topics --create --topic test-logs --bootstrap-server localhost:9092
   ```
3. ? No old Logstash containers: `docker ps | grep logstash`
4. ? Pipeline config directory is clean (auto-generated in temp)

## ?? Expected Test Output

When working correctly:

```
?? Starting Elasticsearch test infrastructure...
? Elasticsearch ready at http://localhost:xxxxx
? Starting Logstash...
? Logstash pipeline created
   ?? Kafka: host.docker.internal:9092
   ?? Elasticsearch: http://elasticsearch:9200
? Logstash ready at http://localhost:xxxxx
? Giving Logstash additional 10 seconds to connect to Kafka...
? Logstash appears to have connected to Kafka
? Kafka producer initialized
?? Sending message to Kafka:
   Topic: logs-test.error
   TraceId: abc-123-def-456
? Message sent to Kafka: Partition 0, Offset 0
? Waiting for Logstash to process message (20 seconds)...
?? Checking Logstash logs for debugging...
   [INFO] Pipeline started successfully
   [INFO] Kafka input plugin started
   [INFO] Subscribed to topics: logs-test.error, test-logs
?? Searching for message in Elasticsearch...
? Found document in Elasticsearch!
   TraceId: abc-123-def-456
   Level: Error
   logstash_processed_at: 2024-01-15T16:45:00Z
   pipeline: kafka-to-elasticsearch
```

## ?? If Test Still Fails

### Debug Step 1: Manually Send to Kafka

```bash
# Send a test message
echo '{"TraceId":"manual-test","Level":"Info","Message":"Manual test"}' | \
  kafka-console-producer --broker-list localhost:9092 --topic test-logs

# Wait 10 seconds, then check Elasticsearch
curl http://localhost:9200/logs-*/_search?pretty
```

If this works but the test doesn't, the issue is in the test timing.

### Debug Step 2: Check Logstash Container Logs Directly

```bash
# Get container ID
docker ps | grep logstash

# View logs
docker logs <container-id> --tail 100 --follow
```

Look for:
- "Pipeline started successfully" ?
- "Subscribed to topics" ?
- "Elasticsearch output" ?

### Debug Step 3: Check Logstash Can Reach Services

```bash
# Check Kafka connectivity
docker exec <logstash-container-id> nc -zv host.docker.internal 9092

# Check Elasticsearch connectivity
docker exec <logstash-container-id> curl http://elasticsearch:9200
```

## ? Summary

**Three critical fixes applied:**

1. ? **Removed redundant JSON parsing** - codec => "json" already parses, no need for json filter
2. ? **Changed auto_offset_reset to "earliest"** - Ensures all messages are consumed
3. ? **Increased wait times** - Gives Logstash time to fully initialize and process

**The test should now pass!** ??

If you're still seeing `searchResponse.Total == 0`, run the diagnostic test first:
```bash
dotnet test --filter "LogstashDiagnosticTests.Step7_End_To_End_Message_Flow"
```

This will give you detailed output showing exactly where the message flow breaks.
