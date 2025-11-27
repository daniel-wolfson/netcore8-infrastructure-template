# Windows Docker Desktop - Cosmos DB Emulator Troubleshooting

## ? Pre-Launch Checklist

Before starting the Cosmos DB Emulator, ensure:

- [x] **Docker Desktop is running** (whale icon in system tray is steady, not animating)
- [x] **Docker is using WSL 2 backend** (Settings ? General ? Use WSL 2 based engine)
- [x] **Docker has sufficient resources**:
  - Memory: At least 4GB (Settings ? Resources ? Advanced)
  - CPU: At least 2 cores
  - Disk: At least 10GB free space
- [x] **Port 8081 is available** (not used by another application)
- [x] **Windows Firewall allows Docker**

---

## ?? Launch Steps

### Method 1: Using Batch Script (Recommended)

```cmd
cd Custom.Framework.Tests\Azure
cosmos-start.bat
```

### Method 2: Using PowerShell

```powershell
cd Custom.Framework.Tests\Azure
.\cosmos.ps1 start
```

### Method 3: Using Docker Compose Directly

```cmd
cd Custom.Framework.Tests\Azure
docker-compose -f docker-compose.cosmos.yml up -d
```

---

## ?? Common Windows-Specific Issues

### Issue 1: Docker Desktop Not Running

**Error:**
```
error during connect: Get "http://%2F%2F.%2Fpipe%2FdockerDesktopLinuxEngine/v1.51/...": 
open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified.
```

**Solutions:**

1. **Start Docker Desktop:**
   - Press `Win` key ? Type "Docker Desktop" ? Press Enter
   - Wait 30-60 seconds for Docker to fully start
   - Verify: System tray whale icon should be steady (not animating)

2. **Verify Docker is running:**
   ```cmd
   docker info
   ```
   Should show Docker system information, not an error.

3. **Restart Docker Desktop:**
   - Right-click whale icon ? Quit Docker Desktop
   - Wait 10 seconds
   - Launch Docker Desktop again

4. **Check Docker service:**
   ```powershell
   # Run as Administrator
   Get-Service -Name com.docker.service
   # Should show "Running"
   
   # If stopped, start it:
   Start-Service -Name com.docker.service
   ```

### Issue 2: WSL 2 Not Enabled

**Error:**
```
WSL 2 installation is incomplete
```

**Solutions:**

1. **Enable WSL 2:**
   ```powershell
   # Run as Administrator
   wsl --install
   wsl --set-default-version 2
   ```

2. **Restart computer**

3. **Verify WSL 2:**
   ```powershell
   wsl --status
   # Should show "Default Distribution: Ubuntu" or similar
   ```

4. **Docker Desktop Settings:**
   - Open Docker Desktop
   - Settings ? General
   - ? Check "Use WSL 2 based engine"
   - Click "Apply & Restart"

### Issue 3: Insufficient Resources

**Error:**
```
Container "cosmos-emulator" exited with code 137
```
(Code 137 = Out of Memory)

**Solutions:**

1. **Increase Docker Memory:**
   - Docker Desktop ? Settings ? Resources ? Advanced
   - Memory: Set to **6GB** or higher
   - Click "Apply & Restart"

2. **Increase Docker CPU:**
   - CPU: Set to **2** or higher

3. **Check available resources:**
   ```powershell
   # Check system memory
   Get-CimInstance Win32_OperatingSystem | Select-Object FreePhysicalMemory
   ```

### Issue 4: Port 8081 Already in Use

**Error:**
```
Bind for 0.0.0.0:8081 failed: port is already allocated
```

**Solutions:**

1. **Find what's using port 8081:**
   ```cmd
   netstat -ano | findstr :8081
   ```

2. **Kill the process:**
   ```powershell
   # Replace <PID> with the Process ID from previous command
   Stop-Process -Id <PID> -Force
   ```

3. **Or change port in docker-compose.cosmos.yml:**
   ```yaml
   ports:
     - "8082:8081"  # Use port 8082 on host instead
   ```

### Issue 5: Image Pull Fails

**Error:**
```
Error response from daemon: Get "https://mcr.microsoft.com/...": dial tcp: lookup mcr.microsoft.com: no such host
```

**Solutions:**

1. **Check internet connection**

2. **Check DNS settings:**
   ```cmd
   nslookup mcr.microsoft.com
   ```

3. **Configure Docker DNS:**
   - Docker Desktop ? Settings ? Docker Engine
   - Add DNS servers:
   ```json
   {
     "dns": ["8.8.8.8", "8.8.4.4"]
   }
   ```

4. **Restart Docker Desktop**

5. **Manual pull:**
   ```cmd
   docker pull mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
   ```

### Issue 6: Line Ending Issues (cosmos-init.sh)

**Error:**
```
/bin/sh: bad interpreter: No such file or directory
```

**Solutions:**

1. **Convert line endings to Unix (LF):**
   - Open `cosmos-init.sh` in VS Code
   - Bottom right corner: Click "CRLF" ? Select "LF"
   - Save file

2. **Or use PowerShell:**
   ```powershell
   $content = Get-Content cosmos-init.sh -Raw
   $content = $content -replace "`r`n", "`n"
   Set-Content cosmos-init.sh -Value $content -NoNewline
   ```

### Issue 7: Windows Firewall Blocking Docker

**Error:**
Container starts but can't access network

**Solutions:**

1. **Allow Docker through firewall:**
   ```powershell
   # Run as Administrator
   New-NetFirewallRule -DisplayName "Docker Desktop" -Direction Inbound -Action Allow -Program "C:\Program Files\Docker\Docker\resources\com.docker.backend.exe"
   ```

2. **Or disable firewall temporarily:**
   - Windows Security ? Firewall & network protection
   - Turn off (for testing only!)

### Issue 8: Container Starts but Health Check Fails

**Symptom:** Container shows "unhealthy"

**Diagnosis:**
```cmd
docker ps
# Shows: (unhealthy)

docker logs cosmos-emulator
# Check for errors
```

**Solutions:**

1. **Wait longer** (emulator needs 2-3 minutes on first start)

2. **Check health manually:**
   ```cmd
   docker exec cosmos-emulator curl -k https://localhost:8081/_explorer/emulator.pem
   ```

3. **Increase health check times in docker-compose.cosmos.yml:**
   ```yaml
   healthcheck:
     interval: 60s       # Increase from 30s
     timeout: 20s        # Increase from 10s
     start_period: 180s  # Increase from 120s
   ```

---

## ?? Step-by-Step Launch Process

### Step 1: Verify Docker Desktop

```cmd
docker info
```

**Expected:** Shows Docker version, containers, images, etc.  
**If error:** See [Issue 1: Docker Desktop Not Running](#issue-1-docker-desktop-not-running)

### Step 2: Check Docker Resources

```powershell
# Docker Desktop ? Settings ? Resources
# Verify:
# - Memory: 4GB+
# - CPU: 2+
# - Disk: 10GB+ free
```

### Step 3: Navigate to Azure Tests Directory

```cmd
cd D:\Projects\DotNetCore\NetCore8.Infrastructure\Custom.Framework.Tests\Azure
```

### Step 4: Start Cosmos Emulator

```cmd
cosmos-start.bat
```

Or manually:
```cmd
docker-compose -f docker-compose.cosmos.yml up -d
```

### Step 5: Monitor Startup (Wait 2-3 minutes)

```cmd
docker logs -f cosmos-emulator
```

**Wait for:** "Started" message (may take 2-3 minutes on first run)

**Press Ctrl+C** to exit log view

### Step 6: Verify Container is Healthy

```cmd
docker ps | findstr cosmos
```

**Expected:**
```
cosmos-emulator   Up 3 minutes (healthy)   0.0.0.0:8081->8081/tcp
```

**If unhealthy:** Wait another minute and check again

### Step 7: Access Data Explorer

Open browser:
```
https://localhost:8081/_explorer/index.html
```

**Expected:** Certificate warning ? Click "Advanced" ? "Proceed to localhost"  
**Then:** Cosmos DB Data Explorer UI should appear

### Step 8: Initialize Database (Optional)

```cmd
cosmos-init.bat
```

Or manually:
```cmd
docker-compose -f docker-compose.cosmos.yml --profile init up
```

### Step 9: Run Tests

```cmd
cd ..
dotnet test --filter "FullyQualifiedName~CosmosDbOrderTests"
```

---

## ?? Diagnostic Commands

### Check Container Status

```cmd
docker ps -a | findstr cosmos
```

### View Logs

```cmd
# Live logs
docker logs -f cosmos-emulator

# Last 100 lines
docker logs --tail 100 cosmos-emulator
```

### Check Container Health

```cmd
docker inspect cosmos-emulator --format='{{.State.Health.Status}}'
```

### Check Resource Usage

```cmd
docker stats cosmos-emulator --no-stream
```

### Test Connectivity

```cmd
# From host
curl -k https://localhost:8081/_explorer/emulator.pem

# From inside container
docker exec cosmos-emulator curl -k https://localhost:8081/_explorer/emulator.pem
```

### Check Network

```cmd
docker network inspect cosmos-network
```

---

## ?? Clean Start Process

If everything fails, try a complete clean start:

### Step 1: Stop Everything

```cmd
cosmos-clean.bat
```

Or manually:
```cmd
docker-compose -f docker-compose.cosmos.yml down -v
docker volume rm cosmos-data
docker network rm cosmos-network
```

### Step 2: Restart Docker Desktop

- Right-click whale icon ? Quit Docker Desktop
- Wait 10 seconds
- Start Docker Desktop
- Wait for initialization (30-60 seconds)

### Step 3: Pull Image Fresh

```cmd
docker pull mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
```

### Step 4: Start Fresh

```cmd
cosmos-start.bat
```

### Step 5: Wait and Monitor

```cmd
docker logs -f cosmos-emulator
```

Wait for "Started" message (2-3 minutes on first run)

---

## ?? Still Not Working?

### Option 1: Use Windows Cosmos DB Emulator (Native)

Instead of Docker, use the native Windows emulator:

```powershell
# Install via Chocolatey
choco install azure-cosmosdb-emulator

# Or download from
# https://aka.ms/cosmosdb-emulator
```

**Advantages:**
- ? No Docker required
- ? Faster startup
- ? Better Windows integration
- ? Full feature parity

**Launch:**
- Start Menu ? "Azure Cosmos DB Emulator"
- Or: `C:\Program Files\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe`

Tests will automatically detect and use Windows emulator!

### Option 2: Increase Docker Timeouts

Edit `docker-compose.cosmos.yml`:

```yaml
healthcheck:
  interval: 60s       # From 30s
  timeout: 30s        # From 10s
  retries: 20         # From 10
  start_period: 300s  # From 120s (5 minutes)
```

### Option 3: Enable Docker Desktop Diagnostics

1. Docker Desktop ? Settings ? Troubleshoot
2. Enable "Send usage statistics"
3. Click "Collect diagnostics"
4. Review diagnostics for errors

---

## ?? Expected Resource Usage (Windows)

| Resource | First Start | Normal |
|----------|-------------|--------|
| **CPU** | 100-200% (2 cores) | 50-100% |
| **Memory** | 2-3 GB | 1.5-2 GB |
| **Disk I/O** | High | Low |
| **Startup Time** | 2-3 minutes | 1-2 minutes |
| **Network** | 50-100 Mbps | < 10 Mbps |

---

## ? Success Indicators

You'll know it's working when:

- [x] `docker ps` shows cosmos-emulator as "healthy"
- [x] Logs show "Started" message
- [x] `https://localhost:8081/_explorer/index.html` loads in browser
- [x] Tests pass: `dotnet test --filter "FullyQualifiedName~CosmosDbOrderTests"`

---

## ?? Getting Help

If issues persist:

1. **Check logs:**
   ```cmd
   cosmos-logs.bat
   ```

2. **Review Docker Desktop logs:**
   - Docker Desktop ? Troubleshoot ? View logs

3. **Check this documentation:**
   - `DOCKER-COSMOS-README.md`
   - `COSMOS-QUICK-REFERENCE.md`

4. **GitHub Issues:**
   - [Cosmos DB Emulator Issues](https://github.com/Azure/azure-cosmos-db-emulator-docker/issues)

---

**Last Updated:** December 2024  
**Tested On:** Windows 11, Docker Desktop 4.25+  
**Cosmos Image:** mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
