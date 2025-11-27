# Cosmos DB Emulator - Quick Reference

## ? Quick Start

### Windows (Batch)
```cmd
cosmos-start.bat        # Start emulator
cosmos-init.bat         # Initialize DB (optional)
cosmos-logs.bat         # View logs
cosmos-stop.bat         # Stop emulator
```

### Windows (PowerShell)
```powershell
.\cosmos.ps1 start      # Start emulator
.\cosmos.ps1 init       # Initialize DB (optional)
.\cosmos.ps1 status     # Check status
.\cosmos.ps1 logs       # View logs
.\cosmos.ps1 stop       # Stop emulator
```

### Linux/macOS
```bash
# Start
docker-compose -f docker-compose.cosmos.yml up -d

# Initialize (optional)
docker-compose -f docker-compose.cosmos.yml --profile init up

# Status
docker ps | grep cosmos-emulator

# Logs
docker logs -f cosmos-emulator

# Stop
docker-compose -f docker-compose.cosmos.yml down
```

---

## ?? Connection Details

```json
{
  "Endpoint": "https://localhost:8081",
  "Key": "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
  "Database": "HospitalityOrders",
  "Container": "Orders"
}
```

**Data Explorer**: https://localhost:8081/_explorer/index.html

---

## ?? Common Commands

### Docker
```bash
# View status
docker ps | grep cosmos

# View logs (live)
docker logs -f cosmos-emulator

# View logs (last 100 lines)
docker logs --tail 100 cosmos-emulator

# Check health
docker inspect cosmos-emulator --format='{{.State.Health.Status}}'

# Restart
docker restart cosmos-emulator

# Remove container + data
docker-compose -f docker-compose.cosmos.yml down -v
```

### Application Configuration
```json
{
  "CosmosDB": {
    "UseEmulator": true,
    "AccountEndpoint": "https://localhost:8081",
    "AccountKey": "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
  }
}
```

---

## ?? Troubleshooting

### Cannot Connect
```bash
# Trust certificate
curl -k https://localhost:8081/_explorer/emulator.pem > emulator.pem

# Windows
Import-Certificate -FilePath .\emulator.pem -CertStoreLocation Cert:\CurrentUser\Root

# Linux
sudo cp emulator.pem /usr/local/share/ca-certificates/emulator.crt
sudo update-ca-certificates
```

### Port in Use
```bash
# Find what's using port 8081
netstat -ano | findstr :8081     # Windows
lsof -i :8081                    # Linux/macOS

# Kill process or change port in docker-compose.cosmos.yml
```

### Slow Startup
```bash
# Expected: 1-2 minutes
# Check progress in logs
docker logs -f cosmos-emulator

# Wait for: "Started"
```

### High CPU/Memory
```yaml
# Adjust in docker-compose.cosmos.yml
resources:
  limits:
    cpus: '1.0'      # Reduce from 2.0
    memory: 2G       # Reduce from 4G
```

---

## ?? Testing

```bash
# Run Cosmos DB tests
cd Custom.Framework.Tests
dotnet test --filter "FullyQualifiedName~Azure"

# Run specific test
dotnet test --filter "FullyQualifiedName~CosmosDbOrderTests"
```

---

## ?? Reset/Clean

### Windows
```cmd
cosmos-clean.bat
```

### PowerShell
```powershell
.\cosmos.ps1 clean
```

### Linux/macOS
```bash
docker-compose -f docker-compose.cosmos.yml down -v
docker volume rm cosmos-data
```

---

## ?? Resource Usage

| Resource | Value |
|----------|-------|
| CPU | ~50-100% (2 cores) |
| Memory | ~2-4 GB |
| Disk | ~5-10 GB |
| Startup | 1-2 minutes |

---

## ?? Help

### Documentation
- `DOCKER-COSMOS-README.md` - Full documentation
- `AzureCosmos-Integration.md` - Integration guide
- `COSMOS-TESTS-README.md` - Testing guide

### Commands
```bash
# Windows
cosmos-start.bat /?

# PowerShell
.\cosmos.ps1 help

# Docker Compose
docker-compose -f docker-compose.cosmos.yml help
```

---

## ?? Useful Links

- [Cosmos DB Emulator Docs](https://learn.microsoft.com/en-us/azure/cosmos-db/docker-emulator-linux)
- [EF Core Cosmos](https://learn.microsoft.com/en-us/ef/core/providers/cosmos/)
- [Docker Hub](https://hub.docker.com/r/microsoft/azure-cosmosdb-emulator)

---

**Quick Reference Version**: 1.0  
**Last Updated**: December 2024  
**Docker Image**: mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
