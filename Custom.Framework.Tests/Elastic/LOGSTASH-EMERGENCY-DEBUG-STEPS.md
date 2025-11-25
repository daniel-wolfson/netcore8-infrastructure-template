# ?? EMERGENCY DEBUG - Why No Documents in Elasticsearch?

## Immediate Actions (While Debugging)

### 1. Check Logstash Logs NOW

Look at the output from this line in your test:
```
Total log lines: {logLines.Length}
```

**What to look for in the logs:**

#### ? SUCCESS Indicators:
```
[INFO ] Pipeline started successfully
[INFO ] Starting kafka input
[INFO ] Subscribed to topics: ["logs-test.error", "test-logs"]
[INFO ] [logstash.inputs.kafka] Kafka version: 3.x.x
```

#### ? FAILURE Indicators:
```
[ERROR] Connection to host.docker.internal:9092 failed
[ERROR] Broker may not be available
[WARN ] Consumer group rebalance failed
[ERROR] Failed to construct kafka consumer
```

### 2. While Paused in Debugger, Run These Commands

#### Command 1: Check Kafka Consumer Group
```bash
kafka-consumer-groups --bootstrap-server localhost:9092 --list
```

**Expected:** You should see `logstash-consumer-group` in the list.

#### Command 2: Check Consumer Group Status
```bash
kafka-consumer-groups --bootstrap-server localhost:9092 \
  --group logstash-consumer-group \
  --describe
```

**What to check:**
- **CURRENT-OFFSET**: Should be > 0 if Logstash consumed messages
- **LAG**: Should be 0 if caught up
- **CONSUMER-ID**: Should show active consumer

**Example of WORKING output:**
```
GROUP                    TOPIC            PARTITION  CURRENT-OFFSET  LOG-END-OFFSET  LAG     CONSUMER-ID
logstash-consumer-group  logs-test.error  0          1               1               0       logstash-0-xxx
```

**Example of BROKEN output:**
```
GROUP                    TOPIC            PARTITION  CURRENT-OFFSET  LOG-END-OFFSET  LAG     CONSUMER-ID
logstash-consumer-group  logs-test.error  0          -               1               -       -
```
(No consumer ID = Logstash never connected)

#### Command 3: Check Message in Kafka
```bash
kafka-console-consumer --bootstrap-server localhost:9092 \
  --topic logs-test.error \
  --from-beginning \
  --max-messages 1
```

**Expected:** You should see your test message JSON.

#### Command 4: Check Elasticsearch Indices
```bash
curl http://localhost:9200/_cat/indices?v | findstr logs
```

**Expected:** `logs-2024.11.24` or similar with doc count > 0

#### Command 5: Check Logstash Container Directly
```bash
# Get container ID
docker ps | findstr logstash

# Check logs for errors
docker logs <container-id> 2>&1 | findstr /I "error failed warn"
```

---

## ?? Most Likely Issues (Ranked by Probability)

### Issue #1: Logstash Never Subscribed to Kafka (70% probability)

**Symptom:** 
- Kafka consumer group shows no active consumers
- Logstash logs don't show "Subscribed to topics"

**Cause:** 
Logstash couldn't connect to `host.docker.internal:9092`

**Test:**
```bash
# From inside Logstash container
docker exec <logstash-container-id> ping -c 2 host.docker.internal
docker exec <logstash-container-id> nc -zv host.docker.internal 9092
```

**Fix if this fails:**
Kafka might not be accessible. Try changing Logstash config to use your actual IP:

```bash
# Get your machine's IP
ipconfig | findstr IPv4

# Update the pipeline to use actual IP instead of host.docker.internal
# e.g., bootstrap_servers => "192.168.1.100:9092"
```

---

### Issue #2: Timing - Logstash Not Ready Yet (60% probability)

**Symptom:**
- First test always fails
- Running test again succeeds
- Consumer group exists but LAG shows messages weren't consumed

**Cause:**
Message sent BEFORE Logstash subscribed to topics

**Test:**
Run the test twice in a row. If second run passes, it's timing.

**Fix:**
Already applied in code (30s wait + 25s wait). But you might need even more:

```csharp
// In InitializeAsync, after creating containers
_output.WriteLine("? Waiting 60 seconds for Logstash to fully subscribe...");
await Task.Delay(60000);  // 1 full minute
```

---

### Issue #3: Pipeline Configuration Error (40% probability)

**Symptom:**
- Logstash subscribed to topics
- Consumer group shows LAG = 0 (consumed messages)
- But no documents in Elasticsearch

**Cause:**
Filter or output error causing documents to be dropped

**Test:**
Check Logstash stdout for Ruby debug output:
```bash
docker logs <logstash-container-id> 2>&1 | findstr "@timestamp"
```

You should see documents being processed with all fields.

**Fix:**
The current config should work. If not, simplify even further:

```ruby
input {
  kafka {
    bootstrap_servers => "host.docker.internal:9092"
    topics => ["logs-test.error"]
    group_id => "logstash-consumer-group"
    codec => "json"
    auto_offset_reset => "earliest"
  }
}

output {
  elasticsearch {
    hosts => ["http://elasticsearch:9200"]
    index => "logs-test"  # Fixed index for debugging
  }
  stdout { codec => rubydebug }
}
```

---

### Issue #4: Elasticsearch Connection Failure (30% probability)

**Symptom:**
- Logstash subscribed and consumed
- Logstash logs show documents processed
- But Elasticsearch errors in logs

**Test:**
```bash
# From Logstash container
docker exec <logstash-container-id> curl http://elasticsearch:9200
```

**Expected:** JSON response with cluster info

**Fix if fails:**
Network issue. Check docker network:
```bash
docker network inspect elastic-test-network
```

Both `logstash` and `elasticsearch` containers should be listed.

---

## ?? Quick Fixes to Try NOW

### Fix #1: Increase Wait Time

Change line 57 in your test:
```csharp
// From:
await Task.Delay(30000);

// To:
await Task.Delay(60000);  // Full minute
_output.WriteLine("? Waited 60 seconds for Logstash");
```

### Fix #2: Use Simplified Pipeline

Create this temp config for testing:

**File: `C:\temp\logstash-test.conf`**
```ruby
input {
  kafka {
    bootstrap_servers => "host.docker.internal:9092"
    topics => ["logs-test.error"]
    group_id => "logstash-test-group"
    codec => "json"
    auto_offset_reset => "earliest"
  }
}

output {
  elasticsearch {
    hosts => ["http://elasticsearch:9200"]
    index => "logs-test"
  }
  stdout { codec => rubydebug }
}
```

Mount it in container:
```csharp
.WithBindMount("C:\\temp", "/usr/share/logstash/pipeline")
```

### Fix #3: Reset Kafka Consumer Group

```bash
# Delete the consumer group
kafka-consumer-groups --bootstrap-server localhost:9092 \
  --group logstash-consumer-group \
  --delete

# Run test again - Logstash will create fresh group
```

---

## ?? Expected vs Actual Analysis

### What SHOULD Happen:

```
1. Test starts ?
2. Logstash container starts (10s) ?
3. Logstash loads pipeline (5s) ?
4. Logstash connects to Kafka (5s) ?? USUALLY FAILS HERE
5. Logstash subscribes to topics (10s)
6. Test waits 30s ?? But step 4 took 20s, so subscription not done!
7. Test sends message ? Logstash not subscribed yet
8. Test waits 25s
9. Message never consumed
10. No documents in ES
```

### What IS Happening (Check Your Logs):

Look at timestamps in Logstash logs:
```
2024-11-24T16:44:33 - Container starts
2024-11-24T16:44:53 - Pipeline starting...
2024-11-24T16:45:?? - Kafka connection attempt???  ?? FIND THIS
2024-11-24T16:45:?? - Subscribed to topics???    ?? OR THIS
```

If you don't see "Subscribed to topics", that's your problem!

---

## ?? ACTION PLAN

### Step 1: While Still Debugging

Evaluate the `logstashLogs` variable in your debugger:
```
// Check if these strings exist in logstashLogs:
hasKafkaStart   = ???
hasSubscribed   = ???
hasElasticsearch = ???
```

### Step 2: Run Consumer Group Check

```bash
kafka-consumer-groups --bootstrap-server localhost:9092 \
  --group logstash-consumer-group \
  --describe
```

Copy the output!

### Step 3: Based on Results

**If no consumer group exists:**
? Logstash never connected to Kafka
? Check `host.docker.internal` connectivity

**If consumer group exists but CURRENT-OFFSET = 0:**
? Logstash connected but didn't consume
? Check `auto_offset_reset` and timing

**If CURRENT-OFFSET > 0 and LAG = 0:**
? Logstash consumed the message!
? Problem is in ES output or indexing
? Check Elasticsearch for documents manually

---

## ?? Debugging Checklist

Run through this checklist:

- [ ] Kafka is running: `kafka-topics --list --bootstrap-server localhost:9092`
- [ ] Topics exist: Should see `logs-test.error` and `test-logs`
- [ ] Message in Kafka: `kafka-console-consumer --topic logs-test.error --from-beginning`
- [ ] Logstash container running: `docker ps | grep logstash`
- [ ] Logstash can ping host: `docker exec <id> ping host.docker.internal`
- [ ] Logstash can reach Kafka: `docker exec <id> nc -zv host.docker.internal 9092`
- [ ] Consumer group exists: `kafka-consumer-groups --list | grep logstash`
- [ ] Consumer is active: `kafka-consumer-groups --describe --group logstash-consumer-group`
- [ ] Logstash logs show "Subscribed": Check `logstashLogs` variable
- [ ] Elasticsearch accessible: `curl http://localhost:9200/_cat/indices`

---

## ?? Nuclear Option - Complete Reset

If nothing works, try complete reset:

```bash
# 1. Stop and remove Logstash container
docker stop <logstash-container-id>
docker rm <logstash-container-id>

# 2. Delete Kafka consumer group
kafka-consumer-groups --bootstrap-server localhost:9092 \
  --group logstash-consumer-group --delete

# 3. Purge Kafka topics
kafka-topics --delete --topic logs-test.error --bootstrap-server localhost:9092
kafka-topics --delete --topic test-logs --bootstrap-server localhost:9092

# 4. Recreate topics
kafka-topics --create --topic logs-test.error --bootstrap-server localhost:9092
kafka-topics --create --topic test-logs --bootstrap-server localhost:9092

# 5. Delete Elasticsearch indices
curl -X DELETE http://localhost:9200/logs-*

# 6. Run test again
```

---

## ?? Information to Collect

Before asking for more help, collect:

1. **Logstash logs** (from debugger variable `logstashLogs`)
2. **Kafka consumer group status**:
   ```bash
   kafka-consumer-groups --bootstrap-server localhost:9092 \
     --group logstash-consumer-group --describe
   ```
3. **Elasticsearch indices**:
   ```bash
   curl http://localhost:9200/_cat/indices?v
   ```
4. **Test output values**:
   - `hasKafkaStart` = ?
   - `hasSubscribed` = ?
   - `hasElasticsearch` = ?
   - `logLines.Length` = ?

Share these 4 items and I can pinpoint the exact issue!

---

## ? TL;DR - Quick Answer

**Most likely:** Logstash hasn't subscribed to Kafka topics yet when your test sends the message.

**Quick test:** Run this BEFORE your test:
```bash
# Wait manually for Logstash
sleep 60

# Check if subscribed
docker logs <logstash-id> 2>&1 | findstr "Subscribed"

# If you see "Subscribed to topics", THEN run test
```

**Permanent fix:** Increase wait time to 60 seconds in `InitializeAsync()` or add a polling check that waits until Logstash logs show "Subscribed to topics".
