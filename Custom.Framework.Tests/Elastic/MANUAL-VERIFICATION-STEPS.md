# Manual Verification Steps - Diagnose Why searchResponse.Total = 0

## ?? Goal
Identify exactly where the message flow breaks: Kafka ? Logstash ? Elasticsearch

---

## Step 1: Verify Kafka Has the Message

### Check if message reached Kafka:

```bash
# List topics
kafka-topics --list --bootstrap-server localhost:9092

# Should show:
# logs-test.error
# test-logs

# Consume from the topic
kafka-console-consumer --bootstrap-server localhost:9092 \
  --topic logs-test.error \
  --from-beginning \
  --max-messages 5
```

**Expected Output:**
```json
{"Timestamp":"2024-01-15T...","Level":"Error","Message":"Test error message...","TraceId":"xxx-xxx-xxx",...}
```

**? If you see the message:** Kafka is working. Problem is with Logstash.  
**? If no message:** Problem is with test sending to Kafka.

---

## Step 2: Check Logstash Consumer Group

### Verify Logstash is consuming:

```bash
kafka-consumer-groups --bootstrap-server localhost:9092 \
  --group logstash-consumer-group \
  --describe
```

**Expected Output:**
```
GROUP                    TOPIC            PARTITION  CURRENT-OFFSET  LOG-END-OFFSET  LAG
logstash-consumer-group  logs-test.error  0          5               5               0
logstash-consumer-group  test-logs        0          3               3               0
```

**Key Indicators:**
- ? **LAG = 0**: Logstash is consuming and caught up
- ? **LAG > 0**: Logstash is falling behind
- ? **No output**: Logstash hasn't subscribed yet or can't connect

---

## Step 3: Check Logstash Container Logs Directly

### Get Logstash container:

```bash
# Find container
docker ps | grep logstash

# View logs (replace <container-id>)
docker logs <container-id> --tail 200
```

### Look for these critical messages:

**? Good Signs:**
```
[INFO ] Pipeline started successfully
[INFO ] Starting kafka input
[INFO ] Subscribed to topics: logs-test.error, test-logs
[INFO ] kafka input plugin started
```

**? Bad Signs:**
```
[ERROR] Connection refused to host.docker.internal:9092
[ERROR] Broker may not be available
[ERROR] Failed to parse JSON
[WARN ] Unable to connect to Elasticsearch
```

---

## Step 4: Test Logstash ? Elasticsearch Connection

### From inside Logstash container:

```bash
# Get container ID
docker ps | grep logstash

# Test Elasticsearch connection
docker exec <logstash-container-id> curl http://elasticsearch:9200

# Should return:
# {
#   "name" : "...",
#   "cluster_name" : "...",
#   "version" : {...}
# }
```

**? If successful:** Logstash can reach Elasticsearch  
**? If fails:** Network issue between containers

---

## Step 5: Check Elasticsearch Directly

### Verify indices exist:

```bash
# List all indices
curl http://localhost:9200/_cat/indices?v

# Should show indices like:
# yellow open logs-2024.01.15 ...
```

### Search all documents:

```bash
# Get all documents from logs-* indices
curl http://localhost:9200/logs-*/_search?pretty -d '{
  "query": {"match_all": {}},
  "size": 10,
  "sort": [{"@timestamp": "desc"}]
}'
```

**? If documents exist:** Logstash IS writing to Elasticsearch  
**? If no documents:** Logstash is NOT writing

---

## Step 6: Manual End-to-End Test

### Send a message manually and trace it:

```bash
# 1. Send to Kafka
echo '{"TraceId":"manual-test-123","Level":"Info","Message":"Manual test message","Timestamp":"2024-01-15T10:00:00Z"}' | \
  kafka-console-producer --broker-list localhost:9092 --topic test-logs

# 2. Wait 10 seconds
sleep 10

# 3. Check Logstash logs
docker logs <logstash-container-id> --tail 50

# 4. Search Elasticsearch
curl http://localhost:9200/logs-*/_search?pretty -d '{
  "query": {
    "match": {
      "TraceId": "manual-test-123"
    }
  }
}'
```

**If this works:** The issue is timing in the test (Logstash not ready when message sent)  
**If this fails:** Configuration or connectivity issue

---

## Step 7: Check Logstash Pipeline Configuration

### View the actual pipeline config:

```bash
# Find the temp directory from test output
# Look for line like: "? Logstash pipeline created at: C:\Temp\logstash-pipeline-xxx"

# View the config
cat C:\Temp\logstash-pipeline-xxx\logstash.conf
```

### Verify it contains:

```ruby
input {
  kafka {
    bootstrap_servers => "host.docker.internal:9092"
    topics => ["logs-test.error", "test-logs"]
    codec => "json"
    auto_offset_reset => "earliest"
  }
}

filter {
  mutate {
    add_field => { 
      "logstash_processed_at" => "%{@timestamp}"
      "pipeline" => "kafka-to-elasticsearch"
    }
  }
}

output {
  elasticsearch {
    hosts => ["http://elasticsearch:9200"]
    index => "logs-%{[@metadata][index_date]}"
  }
  stdout { codec => rubydebug }
}
```

---

## Step 8: Test Kafka Connectivity from Logstash

### Can Logstash reach Kafka?

```bash
# From inside Logstash container
docker exec <logstash-container-id> nc -zv host.docker.internal 9092

# Expected: "Connection to host.docker.internal 9092 port [tcp/*] succeeded!"
```

**? If succeeds:** Network is OK  
**? If fails:** Docker networking issue - Logstash can't reach host

---

## ?? Common Issues and Fixes

### Issue 1: Logstash Starts AFTER Message Sent

**Symptom:** 
- Manual test works
- Automated test fails
- LAG shows messages were never consumed

**Fix:**
```csharp
// Wait BEFORE sending message
await Task.Delay(30000);  // 30 seconds

// THEN send message
await _kafkaProducer.ProduceAsync(...);
```

### Issue 2: `auto_offset_reset => "latest"`

**Symptom:**
- First test fails
- Subsequent tests pass
- Logstash only sees new messages

**Fix:**
```ruby
auto_offset_reset => "earliest"  # Get all messages
```

### Issue 3: Incorrect JSON Parsing

**Symptom:**
- Logstash consumes messages
- No documents in Elasticsearch
- Logstash logs show parsing errors

**Fix:**
Remove redundant JSON filter when using `codec => "json"`

### Issue 4: Index Template Mismatch

**Symptom:**
- Documents in Elasticsearch
- But wrong index name
- Search for `logs-*` returns 0

**Fix:**
```bash
# Check actual index names
curl http://localhost:9200/_cat/indices

# Search that specific index
curl http://localhost:9200/<actual-index-name>/_search
```

---

## ?? Quick Diagnostic Checklist

Run these in order:

```bash
# 1. Kafka running?
kafka-topics --list --bootstrap-server localhost:9092

# 2. Message in Kafka?
kafka-console-consumer --bootstrap-server localhost:9092 --topic logs-test.error --from-beginning --max-messages 1

# 3. Logstash consuming?
kafka-consumer-groups --bootstrap-server localhost:9092 --group logstash-consumer-group --describe

# 4. Logstash logs OK?
docker logs $(docker ps | grep logstash | awk '{print $1}') --tail 100

# 5. Elasticsearch has documents?
curl http://localhost:9200/_cat/indices
curl http://localhost:9200/logs-*/_count

# 6. Can Logstash reach Elasticsearch?
docker exec $(docker ps | grep logstash | awk '{print $1}') curl http://elasticsearch:9200
```

---

## ?? Most Likely Root Causes (Based on Your Symptoms)

### 1. **Timing Issue (90% probability)**

Logstash hasn't fully subscribed to Kafka topics before message is sent.

**Solution:** Increase initial wait time to 30+ seconds before sending message.

### 2. **`auto_offset_reset => "latest"` (80% probability)**

Logstash only sees messages sent AFTER it subscribes.

**Solution:** Change to `auto_offset_reset => "earliest"` (already done in fix).

### 3. **Logstash Pipeline Error (50% probability)**

JSON parsing issue causing documents to be dropped.

**Solution:** Simplified pipeline to remove redundant parsing (already done in fix).

### 4. **Network Connectivity (30% probability)**

Logstash can't reach Kafka at `host.docker.internal:9092`.

**Solution:** Test with `docker exec <logstash> nc -zv host.docker.internal 9092`.

---

## ? After Running Tests

### Collect this information:

1. **Kafka consumer group status:**
   ```bash
   kafka-consumer-groups --bootstrap-server localhost:9092 \
     --group logstash-consumer-group --describe
   ```

2. **Logstash logs (last 200 lines):**
   ```bash
   docker logs <logstash-container-id> --tail 200 > logstash-full.log
   ```

3. **Elasticsearch indices:**
   ```bash
   curl http://localhost:9200/_cat/indices?v > es-indices.txt
   ```

4. **Sample documents:**
   ```bash
   curl http://localhost:9200/logs-*/_search?pretty > es-docs.json
   ```

### Share these files to diagnose the exact issue.

---

## ?? Quick Fix to Try Now

### Modify test to wait longer:

```csharp
public async Task InitializeAsync()
{
    _container = new ElasticTestContainer(_output) { EnableLogstash = true };
    await _container.InitializeAsync();
    
    // ? ADD THIS: Wait for Logstash to fully initialize
    _output.WriteLine("? Waiting 45 seconds for Logstash to fully subscribe...");
    await Task.Delay(45000);
    
    _elasticClient = new ElasticClient(...);
    _kafkaProducer = new ProducerBuilder<string, string>(...).Build();
}
```

This ensures Logstash is fully ready before any tests run.

---

## ?? Need Help?

If test still fails after these checks, provide:
1. Complete Logstash logs (200 lines)
2. Kafka consumer group status
3. Elasticsearch indices list
4. Any error messages from test output

This will pinpoint the exact break point in the pipeline.
