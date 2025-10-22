# Dapr Integration Tests

This directory contains integration tests for Dapr (Distributed Application Runtime).

## ?? Contents

- **DaprTestContainer.cs** - Base infrastructure for running Dapr in Docker using Testcontainers
- **DaprIntegrationTests.cs** - Tests for core Dapr features (State, Pub/Sub, Service Invocation)
- **DaprWebApplicationTests.cs** - Tests for ASP.NET Core application with Dapr sidecar

## ?? What is Dapr?

**Dapr** (Distributed Application Runtime) is a portable, event-driven runtime for building distributed applications across any cloud or on-premise environment.

### Key Features:

1. **State Management** - State management with support for various stores (Redis, PostgreSQL, Cosmos DB, etc.)
2. **Pub/Sub** - Asynchronous messaging between services
3. **Service Invocation** - Method invocation between microservices
4. **Bindings** - Integration with external systems (queues, databases, HTTP)
5. **Actors** - Virtual actors for stateful services
6. **Secrets Management** - Secure secret storage
7. **Observability** - Built-in tracing and metrics

## ??? Test Architecture

```
???????????????????????????????????????????????
?         Testcontainers Infrastructure       ?
?                                             ?
?  ????????????????      ??????????????????? ?
?  ?              ?      ?                 ? ?
?  ?    Redis     ????????  Dapr Sidecar  ? ?
?  ?  (Port 6379) ?      ?  (daprio/daprd) ? ?
?  ?              ?      ?                 ? ?
?  ????????????????      ?  HTTP: 3500     ? ?
?         ?              ?  gRPC: 50001    ? ?
?         ?              ??????????????????? ?
?         ?                      ?           ?
?         ?                      ?           ?
?         ????????????????????????           ?
?                                             ?
?         Test Application / DaprClient       ?
???????????????????????????????????????????????
```

## ?? Required Packages

Ensure the following packages are installed in `Custom.Framework.Tests.csproj`:

```xml
<PackageReference Include="Dapr.Client" Version="1.14.0" />
<PackageReference Include="Dapr.AspNetCore" Version="1.14.0" />
<PackageReference Include="Testcontainers.Redis" Version="3.9.0" />
<PackageReference Include="DotNet.Testcontainers" Version="3.9.0" />
```

## ?? Usage Examples

### 1. Basic State Management Test

```csharp
[Fact]
public async Task StateStore_SaveAndRetrieve_ShouldWork()
{
    // Arrange
    const string storeName = "statestore";
    const string key = "my-key";
    var data = new { Name = "John", Age = 30 };

    // Act
    await _daprClient.SaveStateAsync(storeName, key, data);
    var retrieved = await _daprClient.GetStateAsync<dynamic>(storeName, key);

    // Assert
    Assert.NotNull(retrieved);
    Assert.Equal("John", retrieved.Name);
}
```

### 2. Pub/Sub Test

```csharp
[Fact]
public async Task PubSub_PublishMessage_ShouldSucceed()
{
    // Arrange
    const string pubsubName = "pubsub";
    const string topic = "orders";
    var message = new Order { Id = 123, Total = 99.99m };

    // Act
    await _daprClient.PublishEventAsync(pubsubName, topic, message);

    // Assert - Message published successfully
}
```

### 3. Web Application Test

```csharp
[Fact]
public async Task WebApp_SaveState_ShouldStoreInDapr()
{
    // Arrange
    var request = new { Key = "test", Value = "data" };

    // Act
    var response = await _httpClient.PostAsJsonAsync("/save-state", request);

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

## ?? Dapr Component Configuration

Tests automatically create the following components:

### State Store (Redis)

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.redis
  version: v1
  metadata:
  - name: redisHost
    value: redis:6379
```

### Pub/Sub (Redis)

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.redis
  version: v1
  metadata:
  - name: redisHost
    value: redis:6379
```

## ?? Docker Images

Docker images used:

- **daprio/daprd:1.13.0** - Dapr runtime (sidecar)
- **redis:7-alpine** - Redis for state store and pub/sub
- **daprio/placement** - (optional) for Dapr Actors

## ?? Running Tests

### From Visual Studio

1. Open Test Explorer
2. Select tests in the `Dapr` folder
3. Click "Run"

### From Command Line

```bash
# Run all Dapr tests
dotnet test --filter "FullyQualifiedName~Custom.Framework.Tests.Dapr"

# Run a specific test
dotnet test --filter "FullyQualifiedName~DaprIntegrationTests.StateStore_SaveAndRetrieve_ShouldWork"

# With detailed output
dotnet test --filter "FullyQualifiedName~Custom.Framework.Tests.Dapr" --logger "console;verbosity=detailed"
```

## ?? Requirements

- **Docker Desktop** or **Docker Engine** must be running
- **.NET 8 SDK**
- **Windows** - Docker Desktop with WSL2 or Hyper-V
- **Linux/macOS** - Docker Engine

## ?? Troubleshooting

### Issue: Tests fail with Docker connection error

**Solution:**
```bash
# Check that Docker is running
docker ps

# Check Docker socket availability
docker info
```

### Issue: Ports already in use

**Solution:**
Tests automatically select random ports from the ranges:
- HTTP: 3500-3600
- gRPC: 50001-50100

If the problem persists, stop other containers:
```bash
docker ps
docker stop <container-id>
```

### Issue: Dapr container won't start

**Solution:**
Check container logs:
```bash
docker logs <dapr-container-id>
```

Ensure Redis is running:
```bash
docker ps --filter "ancestor=redis:7-alpine"
```

## ?? Useful Links

- [Dapr Documentation](https://docs.dapr.io/)
- [Dapr .NET SDK](https://github.com/dapr/dotnet-sdk)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- [Dapr Building Blocks](https://docs.dapr.io/concepts/building-blocks-concept/)

## ?? Learning Materials

### Additional Scenarios for Testing:

1. **Actors** - Stateful objects
2. **Bindings** - Integration with external systems
3. **Secrets** - Secret management
4. **Configuration** - Dynamic configuration
5. **Observability** - Tracing and metrics

### Extension Examples:

```csharp
// Actor test example
[Fact]
public async Task Actor_CreateAndInvoke_ShouldWork()
{
    var actorId = new ActorId("test-actor-1");
    var proxy = ActorProxy.Create<IMyActor>(actorId, "MyActor");
    
    await proxy.SetStateAsync("counter", 42);
    var value = await proxy.GetStateAsync("counter");
    
    Assert.Equal(42, value);
}

// Binding test example
[Fact]
public async Task Binding_InvokeOutput_ShouldSendData()
{
    await _daprClient.InvokeBindingAsync(
        "my-binding",
        "create",
        new { message = "Hello from binding!" });
}
```

## ? Best Practices

1. **Test Isolation** - Each test should have unique keys
2. **Cleanup** - Remove test data after execution
3. **Timeouts** - Set reasonable timeouts for asynchronous operations
4. **Logging** - Use `ITestOutputHelper` for debugging
5. **Parallelism** - Dapr tests can run in parallel using different App IDs

## ?? Contributing

When adding new tests:

1. Follow existing code style
2. Add XML comments
3. Group tests by functionality (#region)
4. Document complex scenarios
5. Update this README

---

**Author:** Infrastructure Team  
**Created:** 2024  
**Version:** 1.0
