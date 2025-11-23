# Consul Integration Guide for Custom.Framework (.NET 8)

This guide demonstrates how to integrate HashiCorp Consul for service discovery and health checking in your .NET 8 microservices infrastructure, following the same patterns used for Dapr integration with Testcontainers.

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Installation](#installation)
3. [Local Development with Testcontainers](#local-development-with-testcontainers)
4. [Service Registration](#service-registration)
5. [Service Discovery](#service-discovery)
6. [Health Checks](#health-checks)
7. [Testing](#testing)
8. [Production Deployment](#production-deployment)
9. [Monitoring & Verification](#monitoring--verification)

---

## 🎯 Overview

Consul provides:
- **Service Discovery**: Automatic registration and discovery of microservices
- **Health Checking**: Monitor service health and availability
- **Key/Value Store**: Distributed configuration storage
- **Service Mesh**: Secure service-to-service communication

### Architecture in Custom.Framework

```
┌─────────────────────────────────────────────────────────┐
│ YOUR .NET 8 APPLICATION                                 │
│                                                          │
│  ┌──────────────────────────────────────┐              │
│  │ ASP.NET Core Service                 │              │
│  │ - Registers with Consul on startup   │              │
│  │ - Discovers other services           │              │
│  │ - Reports health status              │              │
│  └──────────────┬───────────────────────┘              │
│                 │                                        │
│                 ▼                                        │
│  ┌──────────────────────────────────────┐              │
│  │ Consul.AspNetCore Client             │              │
│  │ - IConsulClient                      │              │
│  │ - IHealthCheckService                │              │
│  └──────────────┬───────────────────────┘              │
└─────────────────┼──────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────┐
│ CONSUL SERVER (Docker/Cloud)                            │
│                                                          │
│  ┌──────────────────────────────────────┐              │
│  │ Service Catalog                      │              │
│  │ - Registered services                │              │
│  │ - Health status                      │              │
│  │ - Service endpoints                  │              │
│  └──────────────────────────────────────┘              │
│                                                          │
│  ┌──────────────────────────────────────┐              │
│  │ Key/Value Store                      │              │
│  │ - Configuration data                 │              │
│  │ - Feature flags                      │              │
│  └──────────────────────────────────────┘              │
└─────────────────────────────────────────────────────────┘
```

---

## 📦 Installation

### NuGet Packages

Add to your project (`.csproj`):

```xml
<ItemGroup>
  <!-- Consul Client -->
  <PackageReference Include="Consul" Version="1.7.14.3" />
  
  <!-- Health Checks -->
  <PackageReference Include="AspNetCore.HealthChecks.Consul" Version="8.0.1" />
  
  <!-- For Testing -->
  <PackageReference Include="Testcontainers.Consul" Version="3.10.0" />
</ItemGroup>
```

### Quick Docker Setup

For quick local development with Docker:

```bash
# Start Consul server
docker-compose -f docker-compose.consul.yml up -d

# Or use the management script
./scripts/consul-docker.sh start-consul  # Linux/Mac
scripts\consul-docker.bat                # Windows
```

📖 See [CONSUL-DOCKER-QUICKSTART.md](../../CONSUL-DOCKER-QUICKSTART.md) for complete Docker setup guide.

---

## 🧪 Local Development with Testcontainers

### ConsulTestContainer Implementation

Create `Custom.Framework.Tests/Consul/ConsulTestContainer.cs` following the DaprTestContainer pattern:

```csharp
using Consul;
using Docker.DotNet;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Consul;

/// <summary>
/// Consul test infrastructure - creates Consul server for integration tests
/// Similar to DaprTestContainer pattern
/// </summary>
public class ConsulTestContainer(string datacenter = "dc1") : IAsyncLifetime
{
    private readonly string _datacenter = datacenter;
    private DockerClient? _dockerClient;
    private INetwork? _network;
    private IContainer? _consulServer;

    public ITestOutputHelper? Output { get; set; }
    public string HttpPort { get; private set; } = "8500";
    public string DnsPort { get; private set; } = "8600";
    public bool IsForceCleanup { get; set; } = true;

    public string HttpEndpoint => $"http://localhost:{HttpPort}";
    public string WebUIEndpoint => $"{HttpEndpoint}/ui";

    public async Task InitializeAsync()
    {
        try
        {
            Output?.WriteLine($"🚀 Starting Consul datacenter '{_datacenter}'...");

            await StartNetworkAsync();
            await StartConsulServerAsync();

            Output?.WriteLine($"✅ Consul is ready!");
            Output?.WriteLine($"📊 Consul Endpoints:");
            Output?.WriteLine($"  HTTP API: {HttpEndpoint}");
            Output?.WriteLine($"  Web UI: {WebUIEndpoint}");
        }
        catch (Exception ex)
        {
            Output?.WriteLine($"❌ Starting Consul failed. Error: {ex.Message}");
            throw;
        }
    }

    private async Task StartNetworkAsync()
    {
        var networkName = $"consul-{_datacenter}-network";
        _dockerClient = new DockerClientConfiguration().CreateClient();

        var networks = await _dockerClient.Networks
            .ListNetworksAsync(new NetworksListParameters());

        var existingNetwork = networks.FirstOrDefault(n => n.Name == networkName);

        if (existingNetwork == null)
        {
            var createResponse = await _dockerClient.Networks.CreateNetworkAsync(
                new NetworksCreateParameters
                {
                    Name = networkName,
                    CheckDuplicate = true
                });

            _network = new NetworkBuilder()
                .WithName(networkName)
                .Build();

            Output?.WriteLine($"✅ Created network: {networkName}");
        }
        else
        {
            _network = new NetworkBuilder()
                .WithName(networkName)
                .Build();
                
            Output?.WriteLine($"✅ Using existing network: {networkName}");
        }
    }

    private async Task StartConsulServerAsync()
    {
        Output?.WriteLine("⏳ Starting Consul server...");

        var httpPort = 8500;
        var dnsPort = 8600;

        HttpPort = httpPort.ToString();
        DnsPort = dnsPort.ToString();

        _consulServer = new ContainerBuilder()
            .WithImage("hashicorp/consul:1.20")
            .WithName($"consul-{_datacenter}-server")
            .WithNetwork(_network)
            .WithNetworkAliases("consul-server")
            .WithCommand("agent", "-server", "-ui",
                "-bootstrap-expect=1",
                "-client=0.0.0.0",
                $"-datacenter={_datacenter}",
                "-log-level=INFO")
            .WithPortBinding(httpPort, 8500)
            .WithPortBinding(dnsPort, 8600)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(8500)
                    .ForPath("/v1/status/leader")
                    .ForStatusCode(System.Net.HttpStatusCode.OK)))
            .Build();

        await _consulServer.StartAsync();

        // Verify Consul is ready
        await VerifyConsulHealthAsync();

        Output?.WriteLine($"✅ Consul server ready - HTTP: {HttpEndpoint}");
    }

    private async Task VerifyConsulHealthAsync()
    {
        try
        {
            using var client = new ConsulClient(config =>
            {
                config.Address = new Uri(HttpEndpoint);
            });

            var leader = await client.Status.Leader();
            Output?.WriteLine($"✅ Consul leader elected: {leader.Response}");

            var members = await client.Agent.Members(false);
            Output?.WriteLine($"✅ Consul cluster members: {members.Response.Length}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Consul health check failed: {ex.Message}", ex);
        }
    }

    public async Task<IConsulClient> CreateClientAsync()
    {
        var client = new ConsulClient(config =>
        {
            config.Address = new Uri(HttpEndpoint);
            config.Datacenter = _datacenter;
        });

        return await Task.FromResult(client);
    }

    public async Task DisposeAsync()
    {
        Output?.WriteLine($"🛑 Stopping Consul datacenter '{_datacenter}'...");

        if (_consulServer != null)
            await _consulServer.DisposeAsync();

        if (_network != null)
        {
            await _network.DeleteAsync();
            Output?.WriteLine("✅ Network removed");
        }

        _dockerClient?.Dispose();
        Output?.WriteLine($"✅ Consul stopped");
    }
}
```

---

## 🔧 Service Registration

### Automatic Registration on Startup

Create `Custom.Framework/Consul/ConsulServiceRegistration.cs`:

```csharp
using Consul;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Custom.Framework.Consul;

public class ConsulServiceRegistration : IHostedService
{
    private readonly IConsulClient _consulClient;
    private readonly ConsulOptions _options;
    private readonly ILogger<ConsulServiceRegistration> _logger;
    private string? _registrationId;

    public ConsulServiceRegistration(
        IConsulClient consulClient,
        IOptions<ConsulOptions> options,
        ILogger<ConsulServiceRegistration> logger)
    {
        _consulClient = consulClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _registrationId = $"{_options.ServiceName}-{_options.ServiceId}";

        var registration = new AgentServiceRegistration
        {
            ID = _registrationId,
            Name = _options.ServiceName,
            Address = _options.ServiceAddress,
            Port = _options.ServicePort,
            Tags = _options.Tags,
            Check = new AgentServiceCheck
            {
                HTTP = $"http://{_options.ServiceAddress}:{_options.ServicePort}/health",
                Interval = TimeSpan.FromSeconds(10),
                Timeout = TimeSpan.FromSeconds(5),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1)
            }
        };

        await _consulClient.Agent.ServiceRegister(registration, cancellationToken);
        _logger.LogInformation("Service {ServiceId} registered with Consul", _registrationId);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_registrationId != null)
        {
            await _consulClient.Agent.ServiceDeregister(_registrationId, cancellationToken);
            _logger.LogInformation("Service {ServiceId} deregistered from Consul", _registrationId);
        }
    }
}

public class ConsulOptions
{
    public string ServiceName { get; set; } = "my-service";
    public string ServiceId { get; set; } = Guid.NewGuid().ToString();
    public string ServiceAddress { get; set; } = "localhost";
    public int ServicePort { get; set; } = 5000;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string ConsulAddress { get; set; } = "http://localhost:8500";
}
```

### Extension Methods

Create `Custom.Framework/Consul/ConsulExtensions.cs`:

```csharp
using Consul;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Custom.Framework.Consul;

public static class ConsulExtensions
{
    public static IServiceCollection AddConsul(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<ConsulOptions>(configuration.GetSection("Consul"));

        // Register Consul client
        services.AddSingleton<IConsulClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ConsulOptions>>().Value;
            return new ConsulClient(config =>
            {
                config.Address = new Uri(options.ConsulAddress);
            });
        });

        // Register service registration
        services.AddHostedService<ConsulServiceRegistration>();

        return services;
    }
}
```

### Configuration (appsettings.json)

```json
{
  "Consul": {
    "ServiceName": "my-api",
    "ServiceAddress": "localhost",
    "ServicePort": 5000,
    "Tags": ["api", "v1"],
    "ConsulAddress": "http://localhost:8500"
  }
}
```

### Usage in Program.cs

```csharp
using Custom.Framework.Consul;

var builder = WebApplication.CreateBuilder(args);

// Add Consul
builder.Services.AddConsul(builder.Configuration);

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
app.Run();
```

---

## 🔍 Service Discovery

### Discovering Services

```csharp
public class MyService
{
    private readonly IConsulClient _consulClient;

    public MyService(IConsulClient consulClient)
    {
        _consulClient = consulClient;
    }

    public async Task<string> GetServiceEndpointAsync(string serviceName)
    {
        // Get only healthy services
        var services = await _consulClient.Health.Service(serviceName, null, true);
        var service = services.Response.FirstOrDefault();

        if (service != null)
        {
            return $"http://{service.Service.Address}:{service.Service.Port}";
        }

        throw new Exception($"Service '{serviceName}' not found");
    }
}
```

---

## ❤️ Health Checks

### ASP.NET Core Health Checks Integration

```csharp
using HealthChecks.Consul;

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddConsul(setup =>
    {
        setup.HostName = "localhost";
        setup.Port = 8500;
        setup.RequireHttps = false;
    });

app.MapHealthChecks("/health");
```

---

## 🧪 Testing

### Integration Test Example

Create `Custom.Framework.Tests/Consul/ConsulIntegrationTests.cs`:

```csharp
using Consul;
using Xunit;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Consul;

public class ConsulIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ConsulTestContainer _consulContainer = default!;
    private IConsulClient _consulClient = default!;

    public ConsulIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _consulContainer = new ConsulTestContainer("dc1") { Output = _output };
        await _consulContainer.InitializeAsync();

        _consulClient = await _consulContainer.CreateClientAsync();
    }

    [Fact]
    public async Task ServiceRegistration_ShouldSucceed()
    {
        // Arrange
        var registration = new AgentServiceRegistration
        {
            ID = "test-service-1",
            Name = "test-service",
            Address = "localhost",
            Port = 5000
        };

        // Act
        await _consulClient.Agent.ServiceRegister(registration);

        // Assert
        var services = await _consulClient.Agent.Services();
        Assert.Contains(services.Response, s => s.Key == "test-service-1");

        _output.WriteLine($"✅ Service registered: {registration.Name}");
    }

    [Fact]
    public async Task ServiceDiscovery_ShouldFindRegisteredService()
    {
        // Arrange - Register a service
        await _consulClient.Agent.ServiceRegister(new AgentServiceRegistration
        {
            ID = "api-1",
            Name = "my-api",
            Address = "localhost",
            Port = 8080
        });

        // Act - Discover the service
        var healthServices = await _consulClient.Health.Service("my-api");

        // Assert
        Assert.NotEmpty(healthServices.Response);
        var service = healthServices.Response.First();
        Assert.Equal("my-api", service.Service.Service);
        Assert.Equal(8080, service.Service.Port);

        _output.WriteLine($"✅ Service discovered: {service.Service.Address}:{service.Service.Port}");
    }

    [Fact]
    public async Task KeyValue_StoreAndRetrieve_ShouldWork()
    {
        // Arrange
        var key = "config/app/setting";
        var value = "test-value";

        // Act - Store
        await _consulClient.KV.Put(new KVPair(key)
        {
            Value = System.Text.Encoding.UTF8.GetBytes(value)
        });

        // Act - Retrieve
        var result = await _consulClient.KV.Get(key);

        // Assert
        Assert.NotNull(result.Response);
        var retrievedValue = System.Text.Encoding.UTF8.GetString(result.Response.Value);
        Assert.Equal(value, retrievedValue);

        _output.WriteLine($"✅ KV retrieved: {key} = {retrievedValue}");
    }

    public async Task DisposeAsync()
    {
        _consulClient?.Dispose();
        await _consulContainer.DisposeAsync();
    }
}
```

---

## 🚀 Production Deployment

### Docker Compose

See [DOCKER-CONSUL-README.md](../../DOCKER-CONSUL-README.md) for complete Docker setup instructions.

**Quick Start:**

```bash
# Start Consul and services
docker-compose up -d

# View Consul UI
open http://localhost:8500/ui

# Check registered services
curl http://localhost:8500/v1/catalog/services
```

**Basic Docker Compose:**

```yaml
version: '3.8'

services:
  consul:
    image: hashicorp/consul:1.20
    container_name: consul
    command: agent -server -ui -bootstrap-expect=1 -client=0.0.0.0
    ports:
      - "8500:8500"
      - "8600:8600/udp"
    volumes:
      - consul-data:/consul/data
    networks:
      - microservices

  my-api:
    image: my-api:latest
    environment:
      - Consul__ConsulAddress=http://consul:8500
      - Consul__ServiceName=my-api
      - Consul__ServiceAddress=my-api
      - Consul__ServicePort=80
    depends_on:
      - consul
    networks:
      - microservices

networks:
  microservices:
    driver: bridge

volumes:
  consul-data:
```

For complete configuration with PostgreSQL, Redis, and advanced settings, see the root `docker-compose.yml` file.

---

## 📊 Monitoring & Verification

### CLI Commands

```bash
# List all services
consul catalog services

# Get service details
consul catalog nodes -service=my-api

# Check health
consul health checks

# Query KV store
consul kv get config/app/setting

# Watch for service changes
consul watch -type=service -service=my-api
```

### HTTP API

```bash
# List services
curl http://localhost:8500/v1/catalog/services

# Service health
curl http://localhost:8500/v1/health/service/my-api

# KV get
curl http://localhost:8500/v1/kv/config/app/setting
```

### Web UI

Navigate to: [http://localhost:8500/ui](http://localhost:8500/ui)

- View all registered services
- Monitor health checks
- Browse KV store
- View datacenter topology

---

## 🎯 Best Practices

1. **Service IDs**: Use unique IDs for each instance: `{serviceName}-{instanceId}`
2. **Health Checks**: Always configure HTTP health checks with proper intervals
3. **Deregistration**: Set `DeregisterCriticalServiceAfter` to automatically cleanup failed services
4. **Tags**: Use tags for versioning and routing: `["v1", "production", "api"]`
5. **Security**: Enable ACLs and TLS in production environments
6. **High Availability**: Run Consul cluster with 3-5 server nodes
7. **Monitoring**: Integrate with Prometheus/Grafana for metrics

---

## 🔗 Related Documentation

- [Dapr Integration](../Dapr/) - Similar service discovery patterns
- [Kafka Integration](../Kafka/) - Event-driven architecture
- [Redis Integration](../Redis/) - Caching and state management

---

## 📚 References

- [Consul Official Documentation](https://developer.hashicorp.com/consul)
- [Consul .NET Client](https://github.com/G-Research/consuldotnet)
- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- [Consul on Docker](https://developer.hashicorp.com/consul/docs/docker)

---

**Summary**: This guide demonstrates integrating Consul into your .NET 8 Custom.Framework using the same patterns as Dapr - Testcontainers for local development, automatic service registration, health checks, and comprehensive testing strategies.
