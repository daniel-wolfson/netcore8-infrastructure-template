# RabbitMQ Container Test - WebApplicationFactory Pattern Update

## ✅ Update Complete!

`RabbitMQContainerTest.cs` has been refactored to use the same **WebApplicationFactory** pattern as `KafkaTests.cs`.

---

## 🔄 What Changed

### Before (Simple Container Management)
```csharp
public async Task InitializeAsync()
{
    // 1. Start container
    _container = new ContainerBuilder().Build();
    await _container.StartAsync();
    
    // 2. Manually create publisher
    var options = new RabbitMQOptions { /* ... */ };
    _publisher = await RabbitMQPublisher.CreateAsync(options, logger);
}
```

### After (WebApplicationFactory Pattern)
```csharp
public async Task InitializeAsync()
{
    // 1. Start container
    _container = new ContainerBuilder().Build();
    await _container.StartAsync();
    
    // 2. Initialize WebApplicationFactory
    _factory = new WebApplicationFactory<TestProgram>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.ConfigureAppConfiguration(ConfigureTestAppConfiguration);
            builder.ConfigureServices(ConfigureServices);
        });
    
    // 3. Get settings and publisher from DI
    _settings = _factory.Services.GetService<IOptionsMonitor<RabbitMQOptions>>().CurrentValue;
    _publisher = _factory.Services.GetService<IRabbitMQPublisher>();
}
```

---

## 🎯 Key Features Added

### 1. WebApplicationFactory Integration
```csharp
private WebApplicationFactory<TestProgram> _factory = default!;
```

**Like KafkaTests:**
- ✅ Proper DI container
- ✅ Configuration management
- ✅ Service registration
- ✅ Test environment setup

### 2. Configuration Method (from KafkaTests)
```csharp
private void ConfigureTestAppConfiguration(
    WebHostBuilderContext builderContext, 
    IConfigurationBuilder builderConfig)
{
    var directory = Path.GetDirectoryName(typeof(TestHostBase).Assembly.Location)!;
    var env = builderContext.HostingEnvironment;
    
    builderConfig
        .AddJsonFile(Path.Combine(directory, "appsettings.json"), optional: true)
        .AddJsonFile(Path.Combine(directory, $"appsettings.{environmentName}.json"), optional: true)
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMQ:HostName"] = HostName,
            ["RabbitMQ:Port"] = Port.ToString()
        })
        .AddEnvironmentVariables();
}
```

### 3. Service Registration (from KafkaTests)
```csharp
private void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
{
    var configuration = context.Configuration;
    
    // Configure logger
    services.AddSingleton<Serilog.ILogger>(_logger);
    
    // Configure RabbitMQ options
    services.Configure<RabbitMQOptions>(options => { /* ... */ });
    
    // Register publisher
    services.AddSingleton<IRabbitMQPublisher>(sp =>
    {
        var options = sp.GetRequiredService<IOptions<RabbitMQOptions>>().Value;
        var logger = new TestLogger<RabbitMQPublisher>(_output);
        return RabbitMQPublisher.CreateAsync(options, logger).GetAwaiter().GetResult();
    });
}
```

### 4. Settings from DI
```csharp
_settings = _factory.Services
    .GetService<IOptionsMonitor<RabbitMQOptions>>()?.CurrentValue
    ?? throw new ArgumentNullException("RabbitMQOptions not configured");
```

### 5. Publisher from DI
```csharp
_publisher = _factory.Services.GetService<IRabbitMQPublisher>()
    ?? throw new ArgumentNullException("IRabbitMQPublisher not registered");
```

---

## 📋 Pattern Comparison

| Feature | KafkaTests | RabbitMQContainerTest (Now) |
|---------|-----------|----------------------------|
| **WebApplicationFactory** | ✅ | ✅ |
| **TestProgram** | ✅ | ✅ |
| **ConfigureAppConfiguration** | ✅ | ✅ |
| **ConfigureServices** | ✅ | ✅ |
| **ConfigureTestServices** | ✅ | ✅ |
| **DI Options** | ✅ | ✅ |
| **Service Resolution** | ✅ | ✅ |
| **TestHostLogger** | ✅ | ✅ |
| **Disposables List** | ✅ | ✅ |
| **Environment: Test** | ✅ | ✅ |

---

## 🧪 Tests Available

### Container Tests (3)
```csharp
[Fact] Container_ShouldBeRunning()
[Fact] Container_ShouldHaveCorrectPorts()
[Fact] ConnectionString_ShouldBeValid()
```

### Configuration Tests (1 - NEW!)
```csharp
[Fact] Settings_ShouldBeLoadedFromConfiguration()
```

### Publisher Tests (4)
```csharp
[Fact] Publisher_ShouldBeInitializedFromDI()  // Updated!
[Fact] Publisher_ShouldPublishMessage()
[Fact] Publisher_ShouldPublishMultipleMessages()
[Fact] Publisher_ShouldPublishBatch()
```

### Lifecycle Tests (2)
```csharp
[Fact] Container_ShouldRestart()
[Fact] Publisher_ShouldRecoverAfterReconnect()
```

**Total:** 10 tests (1 new!)

---

## 🚀 Run the Tests

```powershell
# Start RabbitMQ container
cd Custom.Framework.Tests\RabbitMQ
rabbitmq-start.bat

# Run container tests
dotnet test --filter "FullyQualifiedName~RabbitMQContainerTest"

# Run specific test
dotnet test --filter "Settings_ShouldBeLoadedFromConfiguration"
dotnet test --filter "Publisher_ShouldBeInitializedFromDI"
```

---

## 📊 Expected Output

```
🐰 Initializing RabbitMQ Container Test with WebApplicationFactory...
   Using AMQP port: 5672
   Using Management port: 15672
✅ RabbitMQ Container started successfully
   Connection: amqp://guest:guest@localhost:5672/
✅ WebApplicationFactory initialized
✅ Configuration loaded
✅ Services configured
✅ RabbitMQ Publisher initialized from DI
✅ RabbitMQ Container Test initialized

Test: Settings_ShouldBeLoadedFromConfiguration
✅ Settings loaded from configuration

Test: Publisher_ShouldBeInitializedFromDI
✅ Publisher initialized from DI and healthy

... (10 tests total)

✅ All tests passed!
```

---

## 🎓 What You Get

### From KafkaTests Pattern

1. **Proper DI Container**
   - Lifetime management
   - Service resolution
   - Options pattern

2. **Configuration System**
   - appsettings.json support
   - Environment-specific config
   - In-memory overrides
   - Environment variables

3. **Test Environment**
   - Isolated test context
   - Test-specific logging
   - Proper cleanup

4. **Professional Structure**
   - Follows .NET conventions
   - Matches other test classes
   - Easy to extend

### New Capabilities

1. **Configuration Testing**
   ```csharp
   [Fact]
   public void Settings_ShouldBeLoadedFromConfiguration()
   {
       _settings.HostName.Should().Be("localhost");
       _settings.Port.Should().Be(5672);
   }
   ```

2. **DI Testing**
   ```csharp
   [Fact]
   public void Publisher_ShouldBeInitializedFromDI()
   {
       _publisher.Should().NotBeNull();
       _publisher.IsHealthy().Should().BeTrue();
   }
   ```

3. **Options Pattern**
   ```csharp
   services.Configure<RabbitMQOptions>(options => { /* ... */ });
   var settings = factory.Services
       .GetService<IOptionsMonitor<RabbitMQOptions>>()
       .CurrentValue;
   ```

---

## 🔧 Technical Details

### Dependencies Added
```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Path = System.IO.Path;
```

### New Fields
```csharp
private readonly Serilog.ILogger _logger;
private readonly List<IDisposable> _disposables = [];
private WebApplicationFactory<TestProgram> _factory = default!;
private RabbitMQOptions _settings = default!;
```

### Disposal Pattern
```csharp
public async Task DisposeAsync()
{
    // 1. Dispose all registered disposables
    foreach (var disposable in _disposables)
    {
        disposable?.Dispose();
    }
    
    // 2. Dispose publisher
    _publisher?.Dispose();
    
    // 3. Dispose container
    await _container?.DisposeAsync();
}
```

---

## ✅ Benefits

### Code Quality
- ✅ **Consistent with KafkaTests** - Same patterns
- ✅ **Professional structure** - Industry standard
- ✅ **Maintainable** - Easy to understand
- ✅ **Extensible** - Easy to add features

### Testing
- ✅ **Better isolation** - Each test independent
- ✅ **Proper DI** - Tests real service resolution
- ✅ **Configuration testing** - Verify settings
- ✅ **Realistic scenarios** - Matches production

### Development
- ✅ **Familiar pattern** - Matches other tests
- ✅ **Documentation** - Well-commented
- ✅ **Debugging** - Better error messages
- ✅ **IDE support** - IntelliSense works better

---

## 📚 Related Files

- ✅ `KafkaTests.cs` - Original pattern source
- ✅ `RabbitMQContainerTest.cs` - Updated to match
- ✅ `RabbitMQPublisherTests.cs` - Shares TestLogger
- ✅ `TestProgram.cs` - Required for WebApplicationFactory

---

## ✅ Build Status

```
Build successful ✅
```

All tests compile and are ready to run!

---

**Pattern Update Complete!** 🎉

`RabbitMQContainerTest` now uses:
- ✅ WebApplicationFactory<TestProgram>
- ✅ Proper configuration system
- ✅ Full DI container
- ✅ Options pattern
- ✅ Test-specific logging
- ✅ Disposables management
- ✅ Same pattern as KafkaTests

Run: `dotnet test --filter "RabbitMQContainerTest"`
