using Consul;
using System.Net;
using Xunit.Abstractions;

namespace Custom.Framework.Tests.Consul;

/// <summary>
/// Integration tests for Consul service discovery and registration
/// Demonstrates service catalog, health checks, and KV store functionality
/// </summary>
public class ConsulIntegrationTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly ITestOutputHelper _output = output;
    private ConsulTestContainer _consulContainer = default!;
    private IConsulClient _consulClient = default!;

    public async Task InitializeAsync()
    {
        _consulContainer = new ConsulTestContainer("dc1") { Output = _output };
        await _consulContainer.InitializeAsync();

        _consulClient = await _consulContainer.CreateClientAsync();
    }

    [Fact]
    public async Task Consul_HealthEndpoint_ShouldBeAccessible()
    {
        // Arrange
        using var httpClient = new HttpClient();

        // Act
        var response = await httpClient.GetAsync($"{_consulContainer.HttpEndpoint}/v1/status/leader");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var leader = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(leader);

        _output.WriteLine($"✅ Consul leader: {leader}");
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
            Port = 5000,
            Tags = ["test", "v1"]
        };

        // Act
        await _consulClient.Agent.ServiceRegister(registration);

        // Assert
        var services = await _consulClient.Agent.Services();
        Assert.Contains(services.Response, s => s.Key == "test-service-1");

        var service = services.Response["test-service-1"];
        Assert.Equal("test-service", service.Service);
        Assert.Equal(5000, service.Port);
        Assert.Contains("test", service.Tags);

        _output.WriteLine($"✅ Service registered: {registration.Name} at {registration.Address}:{registration.Port}");
    }

    [Fact]
    public async Task ServiceDeregistration_ShouldRemoveService()
    {
        // Arrange - Register a service first
        var serviceId = "temp-service";
        await _consulClient.Agent.ServiceRegister(new AgentServiceRegistration
        {
            ID = serviceId,
            Name = "temp-service",
            Address = "localhost",
            Port = 6000
        });

        // Act - Deregister the service
        await _consulClient.Agent.ServiceDeregister(serviceId);

        // Assert
        var services = await _consulClient.Agent.Services();
        Assert.DoesNotContain(services.Response, s => s.Key == serviceId);

        _output.WriteLine($"✅ Service deregistered: {serviceId}");
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
            Port = 8080,
            Tags = new[] { "api", "v1" }
        });

        // Act - Discover the service using catalog
        var catalogServices = await _consulClient.Catalog.Service("my-api");

        // Assert
        Assert.NotEmpty(catalogServices.Response);
        var service = catalogServices.Response.First();
        Assert.Equal("my-api", service.ServiceName);
        Assert.Equal(8080, service.ServicePort);

        _output.WriteLine($"✅ Service discovered via catalog: {service.ServiceAddress}:{service.ServicePort}");
    }

    [Fact]
    public async Task HealthService_ShouldReturnHealthyServices()
    {
        // Arrange - Register a healthy service
        await _consulClient.Agent.ServiceRegister(new AgentServiceRegistration
        {
            ID = "healthy-api",
            Name = "healthy-api",
            Address = "localhost",
            Port = 9000,
            Check = new AgentServiceCheck
            {
                TTL = TimeSpan.FromSeconds(30)
            }
        });

        // Pass the health check
        await _consulClient.Agent.PassTTL("service:healthy-api", "Service is healthy");

        // Act - Get healthy services
        var healthServices = await _consulClient.Health.Service("healthy-api", null, true);

        // Assert
        Assert.NotEmpty(healthServices.Response);
        var service = healthServices.Response.First();
        Assert.Equal("healthy-api", service.Service.Service);

        _output.WriteLine($"✅ Healthy service found: {service.Service.Address}:{service.Service.Port}");
    }

    [Fact]
    public async Task KeyValue_Put_ShouldStoreValue()
    {
        // Arrange
        var key = "config/app/setting";
        var value = "test-value";

        // Act
        var putResult = await _consulClient.KV.Put(new KVPair(key)
        {
            Value = System.Text.Encoding.UTF8.GetBytes(value)
        });

        // Assert
        Assert.True(putResult.Response);

        _output.WriteLine($"✅ KV stored: {key} = {value}");
    }

    [Fact]
    public async Task KeyValue_Get_ShouldRetrieveValue()
    {
        // Arrange
        var key = "config/database/connectionstring";
        var value = "Server=localhost;Database=test";
        await _consulClient.KV.Put(new KVPair(key)
        {
            Value = System.Text.Encoding.UTF8.GetBytes(value)
        });

        // Act
        var result = await _consulClient.KV.Get(key);

        // Assert
        Assert.NotNull(result.Response);
        var retrievedValue = System.Text.Encoding.UTF8.GetString(result.Response.Value);
        Assert.Equal(value, retrievedValue);

        _output.WriteLine($"✅ KV retrieved: {key} = {retrievedValue}");
    }

    [Fact]
    public async Task KeyValue_Delete_ShouldRemoveKey()
    {
        // Arrange
        var key = "temp/setting";
        await _consulClient.KV.Put(new KVPair(key)
        {
            Value = System.Text.Encoding.UTF8.GetBytes("temp-value")
        });

        // Act
        var deleteResult = await _consulClient.KV.Delete(key);

        // Assert
        Assert.True(deleteResult.Response);

        var getResult = await _consulClient.KV.Get(key);
        Assert.Null(getResult.Response);

        _output.WriteLine($"✅ KV deleted: {key}");
    }

    [Fact]
    public async Task KeyValue_List_ShouldReturnKeysWithPrefix()
    {
        // Arrange
        await _consulClient.KV.Put(new KVPair("app/config/setting1") { Value = System.Text.Encoding.UTF8.GetBytes("value1") });
        await _consulClient.KV.Put(new KVPair("app/config/setting2") { Value = System.Text.Encoding.UTF8.GetBytes("value2") });
        await _consulClient.KV.Put(new KVPair("app/config/setting3") { Value = System.Text.Encoding.UTF8.GetBytes("value3") });

        // Act
        var result = await _consulClient.KV.List("app/config/");

        // Assert
        Assert.NotNull(result.Response);
        Assert.True(result.Response.Length >= 3);

        _output.WriteLine($"✅ KV list found {result.Response.Length} keys under 'app/config/'");
        foreach (var kv in result.Response)
        {
            _output.WriteLine($"   - {kv.Key}");
        }
    }

    [Fact]
    public async Task MultipleServices_ShouldBeDiscoverable()
    {
        // Arrange - Register multiple service instances
        await _consulClient.Agent.ServiceRegister(new AgentServiceRegistration
        {
            ID = "web-1",
            Name = "web",
            Address = "192.168.1.10",
            Port = 8080
        });

        await _consulClient.Agent.ServiceRegister(new AgentServiceRegistration
        {
            ID = "web-2",
            Name = "web",
            Address = "192.168.1.11",
            Port = 8080
        });

        await _consulClient.Agent.ServiceRegister(new AgentServiceRegistration
        {
            ID = "web-3",
            Name = "web",
            Address = "192.168.1.12",
            Port = 8080
        });

        // Act
        var services = await _consulClient.Catalog.Service("web");

        // Assert
        Assert.Equal(3, services.Response.Length);

        _output.WriteLine($"✅ Found {services.Response.Length} instances of 'web' service:");
        foreach (var service in services.Response)
        {
            _output.WriteLine($"   - {service.ServiceID}: {service.ServiceAddress}:{service.ServicePort}");
        }
    }

    public async Task DisposeAsync()
    {
        _consulClient?.Dispose();

        if (_consulContainer != null)
        {
            if (_consulContainer.IsForceCleanup)
                await _consulContainer.ForceCleanupNetworksAsync();

            await _consulContainer.DisposeAsync();
        }

        _output.WriteLine("✅ Cleanup complete");
    }
}
