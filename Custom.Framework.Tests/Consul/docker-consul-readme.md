# Docker Configuration for Consul Integration

This directory contains Docker Compose configurations for running Consul with your .NET 8 microservices.

## ?? Files Overview

```
??? docker-compose.yml              # Complete infrastructure (Consul + Services)
??? docker-compose.consul.yml       # Standalone Consul server
??? consul/
?   ??? config/
?       ??? server.json            # Consul server configuration
?       ??? services/              # Service definitions
?           ??? my-api.json        # Example API service
?           ??? database.json      # Example database service
??? README.md                      # This file
```

## ?? Quick Start

### Option 1: Run Complete Infrastructure

```bash
# Start all services (Consul + PostgreSQL + Redis + Your API)
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down
```

### Option 2: Run Consul Only

```bash
# Start only Consul server
docker-compose -f docker-compose.consul.yml up -d

# Stop Consul
docker-compose -f docker-compose.consul.yml down
```

## ?? Configuration

### Consul Server Settings

Edit `consul/config/server.json` to customize:

```json
{
  "datacenter": "dc1",          // Your datacenter name
  "log_level": "INFO",          // DEBUG, INFO, WARN, ERROR
  "bootstrap_expect": 1,        // Number of server nodes
  "enable_script_checks": true  // Allow health check scripts
}
```

### Service Registration

Your .NET 8 application automatically registers with Consul on startup using the configuration in `appsettings.json`:

```json
{
  "Consul": {
    "ServiceName": "my-api",
    "ServiceAddress": "my-api",
    "ServicePort": 80,
    "Tags": ["api", "v1"],
    "ConsulAddress": "http://consul:8500"
  }
}
```

Or via environment variables in `docker-compose.yml`:

```yaml
environment:
  - Consul__ConsulAddress=http://consul:8500
  - Consul__ServiceName=my-api
  - Consul__ServiceAddress=my-api
  - Consul__ServicePort=80
```

## ?? Accessing Services

| Service | URL | Description |
|---------|-----|-------------|
| **Consul UI** | http://localhost:8500/ui | Web interface for service catalog |
| **Consul API** | http://localhost:8500 | HTTP API endpoint |
| **Consul DNS** | localhost:8600 | DNS interface |
| **Your API** | http://localhost:5000 | Your .NET 8 application |
| **PostgreSQL** | localhost:5432 | Database |
| **Redis** | localhost:6379 | Cache/Session store |

## ?? Verifying Consul Setup

### Check Consul Health

```bash
# Check if Consul is running
docker exec consul-server consul members

# List all registered services
docker exec consul-server consul catalog services

# Check specific service health
docker exec consul-server consul catalog nodes -service=my-api
```

### Using Consul HTTP API

```bash
# Get all services
curl http://localhost:8500/v1/catalog/services

# Get service instances
curl http://localhost:8500/v1/catalog/service/my-api

# Get service health
curl http://localhost:8500/v1/health/service/my-api

# Get KV store value
curl http://localhost:8500/v1/kv/config/app/setting
```

## ?? Testing Service Discovery

### Register a Test Service

```bash
# Register service via API
curl -X PUT http://localhost:8500/v1/agent/service/register \
  -d '{
    "ID": "test-service-1",
    "Name": "test-service",
    "Address": "localhost",
    "Port": 9000,
    "Check": {
      "HTTP": "http://localhost:9000/health",
      "Interval": "10s"
    }
  }'
```

### Discover Services from .NET Application

```csharp
// In your application
var consulClient = serviceProvider.GetRequiredService<IConsulClient>();

// Find healthy instances
var services = await consulClient.Health.Service("my-api", null, true);
var endpoint = services.Response.FirstOrDefault();

Console.WriteLine($"Found service at {endpoint.Service.Address}:{endpoint.Service.Port}");
```

## ??? Multi-Node Consul Cluster

For production, run multiple Consul servers:

```yaml
version: '3.8'

services:
  consul-server-1:
    image: hashicorp/consul:1.20
    command: agent -server -bootstrap-expect=3 -ui -client=0.0.0.0
    # ... configuration

  consul-server-2:
    image: hashicorp/consul:1.20
    command: agent -server -retry-join=consul-server-1 -client=0.0.0.0
    # ... configuration

  consul-server-3:
    image: hashicorp/consul:1.20
    command: agent -server -retry-join=consul-server-1 -client=0.0.0.0
    # ... configuration
```

## ?? Security (Production)

### Enable ACLs

```bash
# Bootstrap ACL system
docker exec consul-server consul acl bootstrap

# Save the SecretID token
# Use it in your application configuration
```

### Enable TLS

```yaml
volumes:
  - ./consul/tls/ca.pem:/consul/config/ca.pem:ro
  - ./consul/tls/server-cert.pem:/consul/config/server-cert.pem:ro
  - ./consul/tls/server-key.pem:/consul/config/server-key.pem:ro

environment:
  - CONSUL_HTTP_SSL=true
  - CONSUL_CACERT=/consul/config/ca.pem
  - CONSUL_CLIENT_CERT=/consul/config/server-cert.pem
  - CONSUL_CLIENT_KEY=/consul/config/server-key.pem
```

## ?? Common Commands

```bash
# View Consul logs
docker-compose logs -f consul

# Restart Consul
docker-compose restart consul

# Execute Consul CLI commands
docker exec -it consul-server consul <command>

# Backup Consul data
docker exec consul-server consul snapshot save /consul/data/backup.snap

# Restore from backup
docker exec consul-server consul snapshot restore /consul/data/backup.snap

# Remove all data and start fresh
docker-compose down -v
docker-compose up -d
```

## ?? Troubleshooting

### Consul Not Starting

```bash
# Check logs
docker-compose logs consul

# Check if port 8500 is already in use
netstat -ano | findstr :8500  # Windows
lsof -i :8500                 # Linux/Mac

# Remove old containers and volumes
docker-compose down -v
docker system prune -a
```

### Service Not Registering

1. Check Consul address in your app: `http://consul:8500` (from container) or `http://localhost:8500` (from host)
2. Verify health check endpoint is accessible
3. Check application logs for registration errors
4. Verify network connectivity: `docker network inspect custom-microservices-network`

### Health Check Failing

```bash
# Test health endpoint manually
docker exec my-api curl http://localhost:80/health

# Check Consul's view of the service
curl http://localhost:8500/v1/health/checks/my-api
```

## ?? Additional Resources

- [Consul Official Documentation](https://developer.hashicorp.com/consul)
- [Consul Docker Hub](https://hub.docker.com/_/consul)
- [Custom.Framework Consul Integration](./Custom.Framework/Consul/README.md)
- [Docker Compose Documentation](https://docs.docker.com/compose/)

## ?? Next Steps

1. ? Start Consul: `docker-compose -f docker-compose.consul.yml up -d`
2. ? Access UI: http://localhost:8500/ui
3. ? Configure your .NET app with Consul settings
4. ? Run integration tests
5. ? Deploy to production with HA setup

---

**Note**: For development, the single-server setup is sufficient. For production, use a 3 or 5-server cluster for high availability.
