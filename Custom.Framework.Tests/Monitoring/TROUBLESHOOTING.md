# Troubleshooting: Prometheus Not Starting

## Issue
Prometheus container fails to start or exits immediately in Docker Desktop.

## Common Causes & Solutions

### 1. Health Check Issues (FIXED)
**Problem**: Original health check used `wget` which is not available in Prometheus image.

**Solution**: Updated to use `promtool` which is built into Prometheus:
```yaml
healthcheck:
  test: ["CMD", "promtool", "check", "config", "/etc/prometheus/prometheus.yml"]
```

### 2. Configuration File Errors

**Check prometheus.yml syntax:**
```sh
# From the Monitoring directory
docker run --rm -v ${PWD}/prometheus.yml:/prometheus.yml prom/prometheus:latest promtool check config /prometheus.yml
```

**Common errors:**
- Invalid YAML syntax (tabs instead of spaces)
- Missing required fields
- Invalid scrape configurations

### 3. Volume Mount Issues

**Check if prometheus.yml exists:**
```sh
dir prometheus.yml
```

**If missing, it should be in:**
```
Custom.Framework.Tests\Monitoring\prometheus.yml
```

### 4. Port Already in Use

**Check if port 9090 is in use:**
```powershell
netstat -ano | findstr :9090
```

**Solution**: Change port in Monitoring.yaml:
```yaml
ports:
  - "9091:9090"  # Use 9091 instead
```

### 5. Docker Desktop Issues

**Restart Docker Desktop:**
1. Right-click Docker Desktop tray icon
2. Select "Quit Docker Desktop"
3. Start Docker Desktop again

## Step-by-Step Troubleshooting

### Step 1: Clean Restart

```sh
cd Custom.Framework.Tests\Monitoring

# Stop everything
docker-compose -f Monitoring.yaml down

# Remove volumes
docker-compose -f Monitoring.yaml down -v

# Remove old containers
docker rm -f prometheus grafana 2>nul

# Start fresh
docker-compose -f Monitoring.yaml up -d
```

### Step 2: Check Logs

```sh
# View Prometheus logs
docker-compose -f Monitoring.yaml logs prometheus

# View Grafana logs
docker-compose -f Monitoring.yaml logs grafana

# Follow logs in real-time
docker-compose -f Monitoring.yaml logs -f
```

### Step 3: Check Container Status

```sh
# List containers
docker-compose -f Monitoring.yaml ps

# Check if container is running
docker ps -a | findstr prometheus

# Inspect container
docker inspect prometheus
```

### Step 4: Validate Configuration

```sh
# Check Prometheus config
docker run --rm -v ${PWD}/prometheus.yml:/prometheus.yml prom/prometheus:latest promtool check config /prometheus.yml

# If validation passes, you should see:
# Checking /prometheus.yml
#   SUCCESS: 0 rule files found
```

### Step 5: Manual Start (Debug Mode)

```sh
# Try running Prometheus manually
docker run --rm -p 9090:9090 -v ${PWD}/prometheus.yml:/etc/prometheus/prometheus.yml prom/prometheus:latest

# This will show errors in the console
```

## Quick Fixes

### Fix 1: Remove Health Checks Temporarily

Edit `Monitoring.yaml` and comment out health checks:

```yaml
prometheus:
  # ...other config
  # healthcheck:
  #   test: ["CMD", "promtool", "check", "config", "/etc/prometheus/prometheus.yml"]
```

### Fix 2: Use Absolute Paths

Replace relative paths with absolute paths in `Monitoring.yaml`:

```yaml
volumes:
  - D:/Projects/DotNetCore/NetCore8.Infrastructure/Custom.Framework.Tests/Monitoring/prometheus.yml:/etc/prometheus/prometheus.yml:ro
```

### Fix 3: Simplify prometheus.yml

Create a minimal configuration to test:

```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'prometheus'
    static_configs:
      - targets: ['localhost:9090']
```

## Check If Prometheus Is Working

### Test 1: Access Web UI
```
http://localhost:9090
```

### Test 2: Check Health Endpoint
```sh
curl http://localhost:9090/-/healthy
```

### Test 3: Check Targets
```sh
curl http://localhost:9090/api/v1/targets
```

## Still Not Working?

### Get Detailed Logs

```sh
# Check Docker daemon logs (Windows)
Get-EventLog -LogName Application -Source Docker -Newest 50

# Check container exit code
docker ps -a --filter "name=prometheus" --format "table {{.Status}}"
```

### Common Error Messages

**"Error opening storage"**
```
Solution: Remove prometheus-data volume
docker volume rm prometheus-data
```

**"Permission denied"**
```
Solution: Check file permissions on prometheus.yml
icacls prometheus.yml /grant Everyone:F
```

**"Config file not found"**
```
Solution: Check working directory
cd Custom.Framework.Tests\Monitoring
docker-compose -f Monitoring.yaml up -d
```

## Contact Info

If none of these solutions work:

1. **Check Docker Desktop version**: Settings ? About
2. **Check Docker Compose version**: `docker-compose --version`
3. **Collect logs**: 
   ```sh
   docker-compose -f Monitoring.yaml logs > monitoring-logs.txt
   ```

## Working Configuration

After fixes, your setup should be:

? Prometheus health check uses `promtool`
? Grafana health check uses `curl`
? No service dependency on health checks
? All required directories exist
? Configuration files are valid YAML

## Quick Test Command

Run this to verify everything:

```powershell
cd Custom.Framework.Tests\Monitoring

# Validate config
docker run --rm -v ${PWD}/prometheus.yml:/prometheus.yml prom/prometheus:latest promtool check config /prometheus.yml

# Start services
docker-compose -f Monitoring.yaml up -d

# Wait 10 seconds
Start-Sleep -Seconds 10

# Check status
docker-compose -f Monitoring.yaml ps

# Check Prometheus
curl http://localhost:9090/-/healthy

# Check Grafana
curl http://localhost:3001/api/health
```

If all commands succeed, your monitoring stack is working! ?
