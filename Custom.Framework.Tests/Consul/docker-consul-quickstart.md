# Consul Docker Integration - Quick Reference

## ?? Created Files

### Docker Configuration
- ? `docker-compose.yml` - Complete infrastructure (Consul + PostgreSQL + Redis + Your API)
- ? `docker-compose.consul.yml` - Standalone Consul server
- ? `.env.example` - Environment variables template
- ? `.dockerignore` - Docker build exclusions

### Consul Configuration
- ? `consul/config/server.json` - Consul server settings
- ? `consul/config/services/my-api.json` - Example API service definition
- ? `consul/config/services/database.json` - Example database service definition

### Scripts & Documentation
- ? `scripts/consul-docker.sh` - Linux/Mac management script
- ? `scripts/consul-docker.bat` - Windows management script
- ? `DOCKER-CONSUL-README.md` - Complete Docker setup guide

---

## ?? Quick Start Commands

### Start Consul Server Only
```bash
# Linux/Mac
./scripts/consul-docker.sh start-consul

# Windows
scripts\consul-docker.bat

# Or manually
docker-compose -f docker-compose.consul.yml up -d
```

### Start Complete Infrastructure
```bash
# Linux/Mac
./scripts/consul-docker.sh start-all

# Windows
scripts\consul-docker.bat

# Or manually
docker-compose up -d
```

### Access Consul UI
```
http://localhost:8500/ui
```

---

## ?? Available Services

| Service | Port | URL | Description |
|---------|------|-----|-------------|
| **Consul UI** | 8500 | http://localhost:8500/ui | Service catalog interface |
| **Consul API** | 8500 | http://localhost:8500 | HTTP API |
| **Consul DNS** | 8600 | localhost:8600 | DNS interface |
| **Your API** | 5000 | http://localhost:5000 | .NET 8 application |
| **PostgreSQL** | 5432 | localhost:5432 | Database |
| **Redis** | 6379 | localhost:6379 | Cache/Session |

---

## ?? Configuration in Your .NET App

### appsettings.json
```json
{
  "Consul": {
    "ServiceName": "my-api",
    "ServiceAddress": "my-api",
    "ServicePort": 80,
    "Tags": ["api", "v1", "production"],
    "ConsulAddress": "http://localhost:8500",
    "HealthCheckPath": "/health",
    "HealthCheckInterval": "10s"
  }
}
```

### Program.cs
```csharp
using Custom.Framework.Consul;

var builder = WebApplication.CreateBuilder(args);

// Add Consul service discovery
builder.Services.AddConsul(builder.Configuration);

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
app.Run();
```

---

## ?? Testing

### Check Consul Health
```bash
# Using Docker
docker exec consul-server consul members

# Using HTTP API
curl http://localhost:8500/v1/status/leader

# View all services
curl http://localhost:8500/v1/catalog/services | jq .
```

### Register Test Service
```bash
curl -X PUT http://localhost:8500/v1/agent/service/register \
  -d '{
    "ID": "test-1",
    "Name": "test-service",
    "Address": "localhost",
    "Port": 9000,
    "Tags": ["test"],
    "Check": {
      "HTTP": "http://localhost:9000/health",
      "Interval": "10s"
    }
  }'
```

### Discover Services
```bash
# Get healthy instances
curl http://localhost:8500/v1/health/service/my-api?passing

# Get service details
curl http://localhost:8500/v1/catalog/service/my-api
```

---

## ??? Management Commands

### View Logs
```bash
# Consul logs
docker-compose logs -f consul

# All services
docker-compose logs -f

# Specific service
docker-compose logs -f my-api
```

### Restart Services
```bash
# Restart Consul
docker-compose restart consul

# Restart all
docker-compose restart
```

### Stop Services
```bash
# Stop Consul only
docker-compose -f docker-compose.consul.yml down

# Stop all services
docker-compose down

# Stop and remove volumes
docker-compose down -v
```

---

## ?? Environment Variables

Copy `.env.example` to `.env` and customize:

```bash
cp .env.example .env
```

Key variables:
```env
# Consul
CONSUL_DATACENTER=dc1
CONSUL_ADDRESS=http://consul:8500

# Your Service
APP_NAME=my-api
CONSUL_SERVICE_NAME=my-api
CONSUL_SERVICE_PORT=80

# Database
POSTGRES_DB=mydb
POSTGRES_USER=admin
POSTGRES_PASSWORD=admin123

# Redis
REDIS_HOST=redis
REDIS_PORT=6379
```

---

## ?? Production Considerations

### High Availability
- Run 3 or 5 Consul servers (odd number for quorum)
- Configure `bootstrap_expect=3` or `bootstrap_expect=5`
- Use different availability zones

### Security
1. **Enable ACLs**:
   ```bash
   docker exec consul-server consul acl bootstrap
   ```

2. **Enable TLS**:
   - Generate certificates
   - Mount TLS files in container
   - Configure `CONSUL_HTTP_SSL=true`

3. **Use Secrets**:
   - Store sensitive data in Consul KV with encryption
   - Use Docker secrets or external secret management

### Monitoring
- Enable Prometheus metrics: `/v1/agent/metrics?format=prometheus`
- Integrate with Grafana dashboards
- Set up alerts for service health

---

## ?? Troubleshooting

### Consul Won't Start
```bash
# Check if port is in use
netstat -ano | findstr :8500  # Windows
lsof -i :8500                 # Linux/Mac

# View detailed logs
docker-compose logs consul

# Remove old containers
docker-compose down -v
```

### Service Not Registering
1. Check Consul address in app config
2. Verify health check endpoint works
3. Check Docker network connectivity:
   ```bash
   docker network inspect custom-microservices-network
   ```

### Health Check Failing
```bash
# Test health endpoint
docker exec my-api curl http://localhost:80/health

# Check Consul health status
curl http://localhost:8500/v1/health/checks/my-api
```

---

## ?? Additional Resources

- [Main README](./README.md)
- [Docker Setup Guide](./DOCKER-CONSUL-README.md)
- [Consul Framework Integration](./Custom.Framework/Consul/README.md)
- [Consul Official Docs](https://developer.hashicorp.com/consul)

---

## ? Next Steps

1. Start Consul: `docker-compose -f docker-compose.consul.yml up -d`
2. Access UI: http://localhost:8500/ui
3. Configure your .NET app with Consul settings
4. Run your application
5. Verify service registration in Consul UI
6. Run integration tests
7. Deploy to production with HA setup

---

**?? Tip**: Use the management scripts (`consul-docker.sh` or `consul-docker.bat`) for easier Docker operations!
