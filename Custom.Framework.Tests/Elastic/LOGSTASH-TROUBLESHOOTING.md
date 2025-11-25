# Logstash Troubleshooting Guide

## ?? Common Issues and Solutions

### Issue 1: Logstash Cannot Connect to Kafka

**Symptoms:**
- Test fails with no documents in Elasticsearch
- Logstash logs show: `Connection to node -1 could not be established`
- Kafka consumer group shows no consumers

**Root Cause:**
Logstash container cannot reach Kafka because they're on different networks.

**Solution:**
? **Use `host.docker.internal`** to allow Logstash to reach Kafka on the host machine:

```ruby
# In logstash.conf
input {
  kafka {
    bootstrap_servers => "host.docker.internal:9092"  # ? Correct
    # NOT: "kafka:9092" or "localhost:9092"
  }
}
```

**How to Verify:**
```bash
# Check if Kafka is accessible from inside Logstash container
docker exec <logstash-container-id> ping host.docker.internal

# Check Logstash logs
docker logs <logstash-container-id> | grep -i kafka
```

---

### Issue 2: Kafka Topics Don't Exist

**Symptoms:**
- Logstash starts but doesn't consume messages
- Kafka console consumer shows no messages

**Solution:**
Create the topics before starting the test:

```bash
# Create topics
kafka-topics --create --topic logs-test.error \
  --bootstrap-server localhost:9092 \
  --partitions 3 \
  --replication-factor 1

kafka-topics --create --topic test-logs \
  --bootstrap-server localhost:9092 \
  --partitions 3 \
  --replication-factor 1

# Verify topics exist
kafka-topics --list --bootstrap-server localhost:9092
```

---

### Issue 3: Logstash Pipeline Syntax Error

**Symptoms:**
- Logstash container starts but pipeline doesn't load
- Logs show: `Pipeline configuration invalid`

**Solution:**
Check the Logstash configuration syntax:

```bash
# Get the pipeline config path from test output
# Then validate it:
docker run --rm -v /path/to/config:/config \
  docker.elastic.co/logstash/logstash:8.12.0 \
  logstash -t -f /config/logstash.conf
```

**Common Syntax Errors:**
- Missing quotes around values
- Incorrect Ruby code in filter blocks
- Typos in field names

---

### Issue 4: Messages Reach Kafka but Not Elasticsearch

**Symptoms:**
- Kafka has messages (verified with console consumer)
- Logstash is running
- Elasticsearch has no documents

**Diagnostic Steps:**

#### Step 1: Check Logstash Consumer Group
```bash
kafka-consumer-groups --bootstrap-server localhost:9092 \
  --group logstash-consumer-group \
  --describe
```

**Expected Output:**
```
GROUP                    TOPIC            PARTITION  CURRENT-OFFSET  LOG-END-OFFSET  LAG
logstash-consumer-group  logs-test.error  0          10              10              0
```

If LAG > 0: Logstash is falling behind
If no output: Logstash is not consuming

#### Step 2: Check Logstash Logs
```bash
docker logs <logstash-container-id> --tail 100
```

Look for:
- ? `Pipeline started successfully`
- ? `Kafka input plugin started`
- ? `Connection refused`
- ? `Authentication failed`

#### Step 3: Check Elasticsearch Connection
```bash
# From inside Logstash container
docker exec <logstash-container-id> curl http://elasticsearch:9200
```

---

### Issue 5: Wrong Kafka `auto_offset_reset`

**Symptoms:**
- Test passes sometimes but not always
- Old messages appear but new ones don't

**Problem:**
`auto_offset_reset => "latest"` only processes NEW messages after consumer starts.

**Solution:**
For testing, use `earliest` to consume all messages:

```ruby
input {
  kafka {
    bootstrap_servers => "host.docker.internal:9092"
    topics => ["logs-test.error", "test-logs"]
    auto_offset_reset => "earliest"  # ? For testing
    # auto_offset_reset => "latest"  # ? For production
  }
}
```

---

### Issue 6: JSON Parsing Fails

**Symptoms:**
- Messages reach Elasticsearch but fields are missing
- Logs show: `JSON parse error`

**Solution:**
Check if the message is already JSON or needs parsing:

```ruby
filter {
  # Only parse if 'message' field exists (from Kafka)
  if [message] {
    json {
      source => "message"
      target => "parsed"
    }
    
    # Copy fields to root
    ruby {
      code => "
        parsed = event.get('parsed')
        if parsed.is_a?(Hash)
          parsed.each { |k, v| event.set(k, v) }
          event.remove('parsed')
        end
      "
    }
  }
}
```

---

## ??? Step-by-Step Adjustment Guide

### Step 1: Verify Prerequisites

```bash
# 1. Check Kafka is running
docker ps | grep kafka
# or
kafka-topics --list --bootstrap-server localhost:9092

# 2. Check topics exist
kafka-topics --describe --topic logs-test.error --bootstrap-server localhost:9092

# 3. Check Docker network
docker network ls | grep elastic-test-network
```

### Step 2: Update Logstash Configuration

The key changes made to fix connectivity:

```diff
# In ElasticTestContainer.cs - StartLogstashAsync()

+ .WithExtraHost("host.docker.internal", "host-gateway")
+ .WithEnvironment("LOG_LEVEL", "debug")
- .WithEnvironment("LOG_LEVEL", "info")

# In CreateLogstashPipelineAsync()

- bootstrap_servers => "kafka:9092"
+ bootstrap_servers => "host.docker.internal:9092"

+ session_timeout_ms => "30000"
+ max_poll_interval_ms => "300000"
```

### Step 3: Test Connectivity Manually

Before running tests, verify Logstash can reach Kafka:

```bash
# 1. Start containers
# (run your test once to create containers, then stop before assertions)

# 2. Check Logstash can resolve host.docker.internal
docker exec <logstash-container-id> getent hosts host.docker.internal

# 3. Check Logstash can reach Kafka
docker exec <logstash-container-id> nc -zv host.docker.internal 9092

# 4. Check Logstash pipeline status
curl http://localhost:9600/_node/stats | jq '.pipelines'
```

### Step 4: Monitor the Flow

Open 4 terminals:

**Terminal 1: Kafka Consumer (verify messages arrive)**
```bash
kafka-console-consumer --bootstrap-server localhost:9092 \
  --topic logs-test.error \
  --from-beginning
```

**Terminal 2: Logstash Logs**
```bash
docker logs -f <logstash-container-id>
```

**Terminal 3: Elasticsearch Queries**
```bash
# Watch for new documents
watch -n 2 'curl -s http://localhost:9200/logs-*/_count'
```

**Terminal 4: Run Test**
```bash
dotnet test --filter "KafkaToElasticsearch_ThroughLogstash_ShouldSucceed"
```

### Step 5: Verify Each Stage

#### ? Stage 1: Kafka
```bash
# Send test message
echo '{"TraceId":"test-123","Level":"Error","Message":"Test"}' | \
  kafka-console-producer --broker-list localhost:9092 \
  --topic logs-test.error

# Verify it's there
kafka-console-consumer --bootstrap-server localhost:9092 \
  --topic logs-test.error --from-beginning --max-messages 1
```

#### ? Stage 2: Logstash ? Kafka
```bash
# Check consumer group
kafka-consumer-groups --bootstrap-server localhost:9092 \
  --group logstash-consumer-group \
  --describe

# Should show:
# - Current offset increasing
# - LAG = 0 (caught up)
```

#### ? Stage 3: Logstash ? Elasticsearch
```bash
# Check if Logstash is writing to Elasticsearch
curl http://localhost:9200/logs-*/_search?pretty | jq '.hits.total'

# Check latest document
curl http://localhost:9200/logs-*/_search?pretty -d '
{
  "size": 1,
  "sort": [{"@timestamp": "desc"}]
}' | jq '.hits.hits[0]._source'
```

---

## ?? Quick Checklist

Before running tests, ensure:

- [ ] Kafka is running on `localhost:9092`
- [ ] Topics `logs-test.error` and `test-logs` exist
- [ ] Logstash container has `host.docker.internal` mapped
- [ ] Logstash pipeline uses `host.docker.internal:9092`
- [ ] Elasticsearch is accessible from Logstash
- [ ] Wait time in test is sufficient (15+ seconds)

---

## ?? Expected Log Output

### Successful Flow:

**Kafka Producer:**
```
? Message sent to Kafka: Partition 0, Offset 42
```

**Logstash Logs:**
```
[INFO ] Pipeline started successfully
[INFO ] Starting kafka input plugin
[INFO ] Subscribed to topics: logs-test.error, test-logs
[INFO ] Consumer group: logstash-consumer-group
```

**Elasticsearch:**
```
? Found document in Elasticsearch!
   TraceId: abc-123
   Level: Error
   logstash_processed_at: 2024-01-15T10:30:00Z
   pipeline: kafka-to-elasticsearch
```

---

## ?? Common Error Messages

### Error: `Connection refused to host.docker.internal:9092`

**Cause:** Docker Desktop not configured properly

**Fix:**
1. Ensure Docker Desktop is running
2. Check Docker settings ? Resources ? Enable host networking
3. Restart Docker Desktop

### Error: `Broker may not be available`

**Cause:** Kafka not running or wrong port

**Fix:**
```bash
# Check Kafka status
docker ps | grep kafka

# Check if port 9092 is listening
netstat -an | findstr 9092  # Windows
lsof -i :9092              # Linux/Mac
```

### Error: `Unknown topic or partition`

**Cause:** Topic doesn't exist

**Fix:**
```bash
kafka-topics --create --topic logs-test.error \
  --bootstrap-server localhost:9092
```

---

## ?? Getting Help

If you're still stuck:

1. **Collect diagnostics:**
   ```bash
   # Logstash logs
   docker logs <logstash-container-id> > logstash.log
   
   # Kafka consumer group
   kafka-consumer-groups --bootstrap-server localhost:9092 \
     --group logstash-consumer-group --describe > kafka-consumer.log
   
   # Elasticsearch indices
   curl http://localhost:9200/_cat/indices?v > es-indices.log
   ```

2. **Check the updated test output** - it now includes:
   - Logstash logs (last 50 lines)
   - Existing Elasticsearch indices
   - Sample documents

3. **Review the logs** in this order:
   - Kafka (did message arrive?)
   - Logstash (is it consuming?)
   - Elasticsearch (did document get indexed?)

---

## ? Summary of Fixes Applied

1. ? Changed `kafka:9092` ? `host.docker.internal:9092`
2. ? Added `.WithExtraHost("host.docker.internal", "host-gateway")`
3. ? Increased wait time from 10s ? 15s
4. ? Added Logstash log output to test
5. ? Added diagnostics for missing documents
6. ? Added Kafka session timeout configuration
7. ? Changed log level to `debug` for troubleshooting

**These changes should resolve the connectivity issues!** ??
