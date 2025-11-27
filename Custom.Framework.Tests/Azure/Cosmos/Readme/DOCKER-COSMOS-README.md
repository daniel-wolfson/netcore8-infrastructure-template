# Azure Cosmos DB Emulator - Docker Compose Setup

## Overview

This directory contains Docker Compose configuration and scripts to run Azure Cosmos DB Emulator for local development and testing.

---

## ?? Prerequisites

- **Docker Desktop** installed and running
- **4GB+ RAM** available (Cosmos emulator is resource-intensive)
- **Windows 10/11, Linux, or macOS**

### Resource Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| RAM | 2GB | 4GB |
| CPU | 1 core | 2 cores |
| Disk | 5GB | 10GB |

---

## ?? Quick Start

### Windows

1. **Start Emulator**
   ```cmd
   cosmos-start.bat
   ```

2. **Wait for initialization** (1-2 minutes)

3. **Initialize Database** (optional - app will auto-create)
   ```cmd
   cosmos-init.bat
   ```

4. **Access Data Explorer**
   - Open: https://localhost:8081/_explorer/index.html
   - Accept self-signed certificate warning

### Linux/macOS

```bash
# Start emulator
docker-compose -f docker-compose.cosmos.yml up -d cosmos-emulator

# View logs
docker logs -f cosmos-emulator

# Initialize database (optional)
docker-compose -f docker-compose.cosmos.yml --profile init up cosmos-init

# Stop emulator
docker-compose -f docker-compose.cosmos.yml down
```

---

## ?? Files Included

| File | Description |
|------|-------------|
| `docker-compose.cosmos.yml` | Docker Compose configuration |
| `cosmos-init.sh` | Database initialization script |
| `cosmos-start.bat` | Windows: Start emulator |
| `cosmos-stop.bat` | Windows: Stop emulator |
| `cosmos-init.bat` | Windows: Initialize database |
| `cosmos-clean.bat` | Windows: Clean all data |
| `cosmos-logs.bat` | Windows: View live logs |
| `DOCKER-COSMOS-README.md` | This file |

---

## ?? Configuration

### Default Settings

```yaml
Endpoint: https://localhost:8081
Key: C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==
Database: HospitalityOrders
Container: Orders
Partition Key: /hotelCode
```

### Application Configuration

Update your `appsettings.json`:

```json
{
  "CosmosDB": {
    "UseEmulator": true,
    "AccountEndpoint": "https://localhost:8081",
    "AccountKey": "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
    "DatabaseName": "HospitalityOrders",
    "ContainerName": "Orders"
  }
}
```

---

## ?? Management Commands

### Windows Scripts

```cmd
# Start emulator
cosmos-start.bat

# View logs
cosmos-logs.bat

# Initialize database
cosmos-init.bat

# Stop emulator
cosmos-stop.bat

# Clean all data
cosmos-clean.bat
```

### Docker Commands

```bash
# Start emulator
docker-compose -f docker-compose.cosmos.yml up -d

# View logs
docker logs -f cosmos-emulator

# View logs (last 100 lines)
docker logs --tail 100 cosmos-emulator

# Check health
docker ps | grep cosmos-emulator

# Stop emulator
docker-compose -f docker-compose.cosmos.yml down

# Stop and remove volumes
docker-compose -f docker-compose.cosmos.yml down -v

# Restart emulator
docker-compose -f docker-compose.cosmos.yml restart
```

---

## ?? Access Points

### Data Explorer (Web UI)
- **URL**: https://localhost:8081/_explorer/index.html
- **Features**: Query editor, metrics, settings
- **Note**: Accept browser certificate warning

### REST API Endpoints
- **Primary**: https://localhost:8081
- **Ports**: 10251-10254 (internal emulator endpoints)

### Certificate
- **Download**: https://localhost:8081/_explorer/emulator.pem
- **Location**: Browser will prompt to trust

---

## ?? Verification

### 1. Check Container Status

```bash
docker ps | grep cosmos-emulator
```

Expected output:
```
cosmos-emulator   Up 2 minutes (healthy)   0.0.0.0:8081->8081/tcp
```

### 2. Check Health

```bash
docker inspect cosmos-emulator | grep Health -A 10
```

### 3. Test Connection

```bash
# Linux/macOS
curl -k https://localhost:8081/_explorer/emulator.pem

# Windows (PowerShell)
Invoke-WebRequest -Uri https://localhost:8081/_explorer/emulator.pem -SkipCertificateCheck
```

### 4. View Data Explorer

Open browser: https://localhost:8081/_explorer/index.html

---

## ?? Troubleshooting

### Issue: Container Won't Start

**Symptom:** Container exits immediately

**Solution:**
```bash
# Check logs
docker logs cosmos-emulator

# Ensure enough resources
docker stats cosmos-emulator

# Increase Docker Desktop memory to 4GB+
```

### Issue: Port Already in Use

**Error:** `port 8081 is already allocated`

**Solution:**
```bash
# Find process using port 8081
netstat -ano | findstr :8081    # Windows
lsof -i :8081                   # Linux/macOS

# Stop conflicting process or change port in docker-compose.cosmos.yml
```

### Issue: Cannot Connect to Emulator

**Error:** `The SSL connection could not be established`

**Solution:**
```bash
# Trust emulator certificate
curl -k https://localhost:8081/_explorer/emulator.pem > emulator.pem

# Windows (run as Administrator)
Import-Certificate -FilePath .\emulator.pem -CertStoreLocation Cert:\CurrentUser\Root

# Linux
sudo cp emulator.pem /usr/local/share/ca-certificates/emulator.crt
sudo update-ca-certificates
```

### Issue: Emulator Takes Too Long to Start

**Expected:** 1-2 minutes for full initialization

**Check:**
```bash
# View live logs
docker logs -f cosmos-emulator

# Wait for message: "Started"
```

### Issue: Database/Container Not Found

**Solution:** App will auto-create on first run, or run:
```cmd
cosmos-init.bat         # Windows
docker-compose -f docker-compose.cosmos.yml --profile init up  # Linux/macOS
```

### Issue: High CPU/Memory Usage

**Expected:** Cosmos emulator is resource-intensive

**Resource limits in docker-compose.cosmos.yml:**
```yaml
resources:
  limits:
    cpus: '2.0'
    memory: 4G
```

Adjust as needed for your environment.

---

## ?? Security Notes

### Emulator Key (Not for Production!)

The default emulator key is:
```
C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==
```

?? **WARNING:** This key is publicly known and **MUST NOT** be used in production!

### Self-Signed Certificate

The emulator uses a self-signed certificate. You'll need to:
1. Trust the certificate in your browser
2. Skip SSL validation in development (see app config)

---

## ?? Performance Tips

### 1. Resource Allocation

Increase Docker Desktop resources:
- **Settings** ? **Resources**
- **Memory**: 4GB+ recommended
- **CPU**: 2+ cores recommended

### 2. Use Direct Connection Mode

```json
{
  "CosmosDB": {
    "ConnectionMode": "Direct"
  }
}
```

### 3. Enable Bulk Execution

```json
{
  "CosmosDB": {
    "AllowBulkExecution": true
  }
}
```

### 4. Persistent Data

Data is stored in Docker volume: `cosmos-data`

To clear data:
```bash
docker-compose -f docker-compose.cosmos.yml down -v
```

---

## ?? Testing

### Run Integration Tests

```bash
cd Custom.Framework.Tests
dotnet test --filter "FullyQualifiedName~Azure"
```

### Test Configuration

Tests use `CosmosTestContainer.cs` which:
- ? Detects running emulator
- ? Auto-configures connection
- ? Creates test database/container
- ? Cleans up after tests

---

## ?? Emulator vs Production

### Limitations of Emulator

| Feature | Emulator | Azure Cosmos DB |
|---------|----------|-----------------|
| Scale | Limited | Unlimited |
| Regions | Single | Multi-region |
| Throughput | ~10K RU/s | Unlimited |
| SLA | None | 99.999% |
| Features | Most | All |

### When to Use Production

- ? Multi-region testing
- ? Performance/scale testing
- ? Advanced features (geo-replication, multi-master)
- ? CI/CD pipelines (use Free Tier)

---

## ?? Additional Resources

- [Cosmos DB Emulator Docs](https://learn.microsoft.com/en-us/azure/cosmos-db/docker-emulator-linux)
- [EF Core Cosmos Provider](https://learn.microsoft.com/en-us/ef/core/providers/cosmos/)
- [Cosmos DB Best Practices](https://learn.microsoft.com/en-us/azure/cosmos-db/best-practice-dotnet)

---

## ?? Upgrade

To update to latest emulator version:

```bash
# Pull latest image
docker pull mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest

# Restart
docker-compose -f docker-compose.cosmos.yml up -d --force-recreate
```

---

## ?? Support

For issues:
1. Check logs: `cosmos-logs.bat` or `docker logs cosmos-emulator`
2. Review [Troubleshooting](#-troubleshooting) section
3. Check [Docker Hub](https://hub.docker.com/r/microsoft/azure-cosmosdb-emulator) for known issues
4. Verify Docker Desktop is running and has sufficient resources

---

**Created:** December 2024  
**Docker Image:** mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest  
**Framework:** .NET 8  
**Tested On:** Windows 11, Ubuntu 22.04, macOS Monterey
