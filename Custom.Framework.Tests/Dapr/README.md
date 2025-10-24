# Dapr Integration Tests

This directory contains integration tests for Dapr (Distributed Application Runtime).

## ?? Contents

- **DaprTestContainer.cs** - Unified Dapr stack infrastructure (creates "dapr" compose stack)
- **DaprIntegrationTests.cs** - Tests for core Dapr features (State, Pub/Sub, Service Invocation)
- **DaprWebApplicationTests.cs** - Tests for ASP.NET Core application with Dapr sidecar

## ?? Docker Desktop Stack View

After starting Dapr tests, you'll see in Docker Desktop:

```
?? dapr (2 containers)
   ??? ?? dapr-redis
   ??? ?? dapr-sidecar
```

**Just like your "monitoring" and "kafka" stacks!**

This unified stack approach:
? Groups Dapr containers together in Docker Desktop  
? Uses shared network for container communication  
? Provides clean, organized view of infrastructure  
? Easy to start/stop entire stack at once  

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
???????????????????????????????????????
?         Dapr Stack       ?
?       (dapr-network)                ?
???????????????????????????????????????
?         ?
?  ????????????      ??????????????? ?
?  ?  Redis   ???????? Dapr Sidecar? ?
?  ?  :6379   ?      ? daprio/daprd? ?
?  ???????????? ? HTTP: 3500  ? ?
?          ? gRPC: 50001 ? ?
?  ??????????????? ?
?        ?         ?
???????????????????????????????????????
        ?
            Test Application
            (DaprClient)
```

## ?? Required Packages

Ensure the following packages are installed in `Custom.Framework.Tests.csproj`:

```xml
<PackageReference Include="Dapr.Client" Version="1.14.0" />
<PackageReference Include="Dapr.AspNetCore" Version="1.14.0" />
<PackageReference Include="Testcontainers.Redis" Version="4.7.0" />
<PackageReference Include="DotNet.Testcontainers" Version="4.7.0" />
```

## ?? Usage Examples

### 1. Basic Test Setup

```csharp
public class DaprIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private DaprTestContainer _daprContainer = default!;
    private DaprClient _daprClient = default!;

    public DaprIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
   // Start Dapr stack (creates "dapr" compose stack in Docker Desktop)
        _daprContainer = new DaprTestContainer(_output);
      await _daprContainer.InitializeAsync();

        // Create Dapr client
        _daprClient = new DaprClientBuilder()
       .UseHttpEndpoint(_daprContainer.DaprHttpEndpoint)
  .UseGrpcEndpoint(_daprContainer.DaprGrpcEndpoint)
         .Build();
    }

    [Fact]
    public async Task StateStore_SaveAndRetrieve_ShouldWork()
  {
        // Your test here...
    }

    public async Task DisposeAsync()
    {
      await _daprContainer.DisposeAsync();
    }
}
```

### 2. Custom Stack Name

```csharp
// Creates "my-dapr-tests" stack in Docker Desktop
_daprContainer = new DaprTestContainer(_output, appId: "testapp", stackName: "my-dapr-tests");
```

Result in Docker Desktop:
```
?? my-dapr-tests (2 containers)
   ??? ?? my-dapr-tests-redis
   ??? ?? my-dapr-tests-sidecar
```

### 3. State Management Test

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

### 4. Pub/Sub Test

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

- **daprio/daprd:1.14.4** - Dapr runtime (sidecar)
- **redis:7.0** - Redis for state store and pub/sub

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

## ?? Viewing in Docker Desktop

While tests are running, open Docker Desktop and you'll see:

```
Containers
??? ?? dapr (2 containers)
?   ??? ?? dapr-redis    (Port 6379)
?   ??? ?? dapr-sidecar(Ports 3500, 50001)
```

**Same organization as your monitoring and kafka stacks!**

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

### Issue: Containers not showing as "dapr" stack

**Solution:**
Ensure the `stackName` parameter is set correctly:
```csharp
_daprContainer = new DaprTestContainer(_output, stackName: "dapr");
```

All containers must use the same prefix: `dapr-redis`, `dapr-sidecar`

### Issue: Dapr container won't start

**Solution:**
Check container logs:
```bash
docker logs dapr-sidecar
```

Ensure Redis is running:
```bash
docker ps --filter "name=dapr-redis"
```

## ?? Useful Links

- [Dapr Documentation](https://docs.dapr.io/)
- [Dapr .NET SDK](https://github.com/dapr/dotnet-sdk)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- [Dapr Building Blocks](https://docs.dapr.io/concepts/building-blocks-concept/)

## ? Best Practices

1. **Use consistent stack naming** - All containers get same prefix
2. **Start containers in parallel** - Faster test initialization
3. **Use shared network** - Enable container communication
4. **Test isolation** - Each test should have unique keys
5. **Cleanup** - Always dispose containers properly
6. **Logging** - Use `ITestOutputHelper` for debugging

1. 📝 Summary Table
Parameter	Value	Meaning
--app-port	"5000"	App listens on port 5000
--app-port	"0"	No app (API-only mode)
--app-address	"localhost" (default)	App is on same machine as Dapr
--app-address	"host.docker.internal"	App is on Docker host machine
--app-address	"myapp"	App is a container named "myapp"
--app-address	"192.168.1.100"	App is at specific IP address
Your configuration combines both to create a bridge between Dapr (in Docker) and your app (on Windows)!

## ?? Summary

This Dapr test infrastructure:
- ? Creates unified "dapr" stack in Docker Desktop
- ? Groups Redis and Dapr sidecar containers together
- ? Provides shared network for communication
- ? Easy to start/stop entire stack
- ? Clean organization matching monitoring/kafka patterns

**Result: Professional Dapr test infrastructure! ??**
