# ?? CRITICAL ERROR LOCATION - Logstash Cannot Connect to Kafka

## ?? **ERROR LOCATION**

**File:** `Custom.Framework.Tests\Elastic\ElasticTestContainer.cs`  
**Method:** `CreateLogstashPipelineAsync()`  
**Line:** ~235 (in the Logstash pipeline configuration)

```ruby
# THE PROBLEM IS HERE:
bootstrap_servers => "host.docker.internal:9092"
```

## ? **WHY THIS FAILS**

### Root Cause:
Logstash container **cannot reach Kafka** at `host.docker.internal:9092` because:

1. **`host.docker.internal` doesn't resolve** on your Docker setup
2. **Kafka is not listening** on all interfaces (only localhost)
3. **Firewall blocking** the connection
4. **Docker networking misconfiguration**

### Evidence:
```bash
curl http://localhost:9200/logs-*/_count
# Returns: {"error": {"type": "index_not_found_exception"}}
```

This proves:
- ? NO indices created = NO documents indexed
- ? NO documents indexed = Logstash never sent anything to Elasticsearch  
- ? Logstash never sent anything = Logstash never consumed from Kafka
- ? Never consumed from Kafka = **CONNECTION FAILED**

---

## ? **SOLUTION 1: Verify host.docker.internal (RECOMMENDED)**

### Step 1: Check from Logstash Container

```powershell
# Get Logstash container ID from your test output
docker ps | findstr logstash

# Test DNS resolution
docker exec <container-id> ping -c 2 host.docker.internal

# Test port connectivity
docker exec <container-id> nc -zv host.docker.internal 9092
```

**Expected Output:**
```
Connection to host.docker.internal 9092 port [tcp/*] succeeded!
```

**If this FAILS:**
- `host.docker.internal` doesn't work on your setup
- Use Solution 2 or 3

---

## ? **SOLUTION 2: Use Your Actual Machine IP**

### Find Your IP:

```powershell
ipconfig
# Look for "IPv4 Address" under your active network adapter
# Example: 192.168.1.100
```

### Update Code:

Change line ~235 in `CreateLogstashPipelineAsync()`:

```ruby
# FROM:
bootstrap_servers => "host.docker.internal:9092"

# TO (replace with YOUR IP):
bootstrap_servers => "192.168.1.100:9092"
```

### Configure Kafka to Listen on All Interfaces:

Edit your Kafka `server.properties`:

```properties
# FROM:
listeners=PLAINTEXT://localhost:9092

# TO:
listeners=PLAINTEXT://0.0.0.0:9092
advertised.listeners=PLAINTEXT://192.168.1.100:9092
```

Restart Kafka after changing.

---

## ? **SOLUTION 3: Put Kafka in Docker Too**

### Option A: Use Testcontainers for Kafka

Add Kafka container to your test setup:

```csharp
private IContainer? _kafkaContainer;

private async Task StartKafkaAsync()
{
    _kafkaContainer = new ContainerBuilder()
        .WithImage("confluentinc/cp-kafka:7.5.0")
        .WithNetwork(_network)
        .WithNetworkAliases("kafka")
        .WithPortBinding(9092, true)
        .WithEnvironment("KAFKA_BROKER_ID", "1")
        .WithEnvironment("KAFKA_ZOOKEEPER_CONNECT", "zookeeper:2181")
        .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", "PLAINTEXT://kafka:9092")
        .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
        .Build();
        
    await _kafkaContainer.StartAsync();
}
```

Then use `kafka:9092` instead of `host.docker.internal:9092`.

---

## ? **SOLUTION 4: Fix Applied - Dynamic IP Detection**

The code now tries to detect your local IP automatically:

```csharp
private string GetKafkaBootstrapServers()
{
    var hostName = System.Net.Dns.GetHostName();
    var addresses = System.Net.Dns.GetHostAddresses(hostName);
    
    var localIP = addresses
        .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork 
                           && !IPAddress.IsLoopback(ip));
    
    if (localIP != null)
    {
        _output?.WriteLine($"Found local IP: {localIP}");
        return $"{localIP}:9092";  // Use actual IP as fallback
    }
    
    return "host.docker.internal:9092";  // Default
}
```

---

## ?? **DIAGNOSTIC STEPS**

### Step 1: Verify Kafka is Running

```powershell
# Should list your topics
kafka-topics --list --bootstrap-server localhost:9092
```

**Expected:** `logs-test.error` and `test-logs` appear

**If FAILS:** Kafka is not running or not on port 9092

### Step 2: Check What Kafka is Listening On

```powershell
netstat -an | findstr 9092
```

**Expected:**
```
TCP    0.0.0.0:9092          0.0.0.0:0              LISTENING
# or
TCP    127.0.0.1:9092        0.0.0.0:0              LISTENING
```

**If shows `127.0.0.1`:** Kafka only listens on localhost (see Solution 2)

### Step 3: Test from Your Machine

```powershell
# Send a test message
echo '{"test":"message"}' | kafka-console-producer --broker-list localhost:9092 --topic test-logs

# Verify it arrived
kafka-console-consumer --bootstrap-server localhost:9092 --topic test-logs --from-beginning --max-messages 1
```

**If WORKS:** Kafka is fine, issue is Docker networking

### Step 4: Check Docker Network

```powershell
docker network inspect elastic-test-network
```

Look for `logstash` and `elasticsearch` containers listed.

### Step 5: Test from Logstash Container

```powershell
# Get container ID
docker ps | findstr logstash

# Try to reach Kafka
docker exec <container-id> sh -c "echo '' | nc -zv host.docker.internal 9092"
```

**If FAILS:** Networking issue confirmed

---

## ?? **QUICK FIX TO TRY NOW**

### Option A: Manual IP Override

Create environment variable:

```powershell
# Set your machine's IP
$env:KAFKA_IP="192.168.1.100"

# Or hardcode in ElasticTestContainer.cs line ~272:
return "192.168.1.100:9092";  // Replace with YOUR IP
```

### Option B: Docker Desktop Settings

1. Open Docker Desktop
2. Go to Settings ? Resources ? Network
3. Ensure "Use host networking" is **disabled**
4. Restart Docker Desktop

### Option C: Windows Firewall

```powershell
# Allow inbound on port 9092
New-NetFirewallRule -DisplayName "Kafka" -Direction Inbound -LocalPort 9092 -Protocol TCP -Action Allow
```

---

## ?? **HOW TO CONFIRM IT'S FIXED**

### Success Indicators:

1. **Test output shows:**
   ```
   ? Logstash subscribed to Kafka topics after X seconds
   ```

2. **Elasticsearch has indices:**
   ```powershell
   curl http://localhost:9200/_cat/indices
   # Should show: logs-2024.11.24
   ```

3. **Consumer group exists:**
   ```powershell
   kafka-consumer-groups --bootstrap-server localhost:9092 --group logstash-consumer-group --describe
   # Should show CURRENT-OFFSET > 0 and LAG = 0
   ```

4. **Documents in Elasticsearch:**
   ```powershell
   curl http://localhost:9200/logs-*/_count
   # Should return: {"count": 1, ...}
   ```

---

## ?? **RECOMMENDED ACTION PLAN**

### Plan A: Quick Test (5 minutes)

1. Run the diagnostic commands above
2. If `host.docker.internal` ping fails, hardcode your IP
3. Restart test

### Plan B: Proper Fix (15 minutes)

1. Configure Kafka to listen on all interfaces
2. Add firewall rule for port 9092
3. Use actual IP in bootstrap_servers
4. Create Kafka container in Docker network

### Plan C: Nuclear Option (30 minutes)

1. Put Kafka in Docker using Testcontainers
2. Use same Docker network as Elasticsearch/Logstash
3. Change bootstrap_servers to `kafka:9092`
4. No host networking issues

---

## ?? **SUMMARY**

| Problem | Solution | Difficulty |
|---------|----------|------------|
| `host.docker.internal` doesn't resolve | Use actual IP | Easy |
| Kafka only on localhost | Configure to listen on 0.0.0.0 | Medium |
| Firewall blocking | Add firewall rule | Easy |
| Docker networking | Put Kafka in Docker too | Medium |

**MOST LIKELY FIX:** Replace `host.docker.internal:9092` with your machine's actual IP address (e.g., `192.168.1.100:9092`).

---

## ?? **STILL NOT WORKING?**

Provide these outputs:

```powershell
# 1. Kafka status
kafka-topics --list --bootstrap-server localhost:9092

# 2. Network status
netstat -an | findstr 9092

# 3. Your IP
ipconfig | findstr IPv4

# 4. Docker test
docker ps | findstr logstash
docker exec <id> ping -c 2 host.docker.internal

# 5. Logstash logs (from test output)
# Copy the "Last 50 log lines" section
```

Share these 5 items and we can pinpoint the exact issue!
