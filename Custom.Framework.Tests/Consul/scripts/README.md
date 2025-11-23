# Docker Management Scripts

This folder contains utility scripts for managing Docker containers and networks.

## ?? Scripts

### 1. Consul Management
- **`consul-docker.sh`** (Linux/Mac) - Interactive menu for Consul Docker operations
- **`consul-docker.bat`** (Windows) - Interactive menu for Consul Docker operations

**Usage:**
```bash
# Linux/Mac
chmod +x consul-docker.sh
./consul-docker.sh

# Windows
consul-docker.bat
```

**Features:**
- Start/Stop Consul server
- Start/Stop complete infrastructure
- View status and logs
- Cleanup volumes

---

### 2. Network Cleanup
- **`cleanup-docker-network.sh`** (Linux/Mac) - Fix network conflicts
- **`cleanup-docker-network.ps1`** (Windows) - Fix network conflicts

**Usage:**
```bash
# Linux/Mac
chmod +x cleanup-docker-network.sh
./cleanup-docker-network.sh

# Windows PowerShell
.\cleanup-docker-network.ps1
```

**When to use:**
- Error: "Pool overlaps with other one on this address space"
- Network already exists conflicts
- Docker Compose won't start due to network issues

---

## ?? Quick Reference

### Start Consul Only
```bash
# Interactive
./consul-docker.sh  # or consul-docker.bat

# Direct command
docker-compose -f ../Custom.Framework.Tests/Consul/docker-compose.yml up -d
```

### Start All Services
```bash
docker-compose -f ../Custom.Framework.Tests/Consul/docker-compose.yml up -d
```

### Clean Up Networks
```bash
# If you see network errors
./cleanup-docker-network.sh  # or .ps1 for Windows
```

### View Logs
```bash
docker-compose -f ../Custom.Framework.Tests/Consul/docker-compose.yml logs -f consul
```

### Stop Everything
```bash
docker-compose -f ../Custom.Framework.Tests/Consul/docker-compose.yml down
```

---

## ?? Making Scripts Executable (Linux/Mac)

```bash
chmod +x consul-docker.sh
chmod +x cleanup-docker-network.sh
```

---

## ?? Documentation

- [Docker Consul README](../DOCKER-CONSUL-README.md) - Complete Docker setup guide
- [Consul Quickstart](../CONSUL-DOCKER-QUICKSTART.md) - Quick reference
- [Network Troubleshooting](../DOCKER-NETWORK-TROUBLESHOOTING.md) - Fix network issues

---

## ?? Tips

1. **Use the interactive menus** - They're easier than remembering commands
2. **Run cleanup scripts first** - If you encounter any errors
3. **Check logs** - `docker-compose logs -f` is your friend
4. **Use Makefile** - For even simpler commands: `make consul-start`
