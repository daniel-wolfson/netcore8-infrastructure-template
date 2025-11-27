# Connection Mode Configuration Fix

## Problem

```
System.ArgumentException: 'MaxTcpConnectionsPerEndpoint requires ConnectionMode to be set to Direct'
```

This error occurred because the code was trying to set Direct mode connection settings (`MaxTcpConnectionsPerEndpoint` and `MaxRequestsPerTcpConnection`) while using **Gateway mode** for the Cosmos DB Emulator.

---

## Root Cause

### Invalid Configuration Sequence

The original code had this flow:

1. ? Set connection mode to Direct (for production)
2. ? Configure Direct mode settings (`MaxTcpConnectionsPerEndpoint`)
3. ? **Override** connection mode to Gateway (for emulator)
4. ? **Result:** Gateway mode with Direct mode settings = Exception

### Why It Fails

From [Microsoft.Azure.Cosmos documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos.cosmosclientoptions):

> `MaxTcpConnectionsPerEndpoint` and `MaxRequestsPerTcpConnection` are **only valid for Direct connection mode**.

When `ConnectionMode` is set to `Gateway`, these properties throw an `ArgumentException`.

---

## Solution Applied

### Fixed Configuration Sequence

The corrected code now follows this pattern:

```csharp
if (_options.UseEmulator)
{
    // Emulator: Use Gateway mode (required for SSL bypass)
    cosmosOptions.ConnectionMode(Microsoft.Azure.Cosmos.ConnectionMode.Gateway);
    
    // SSL bypass for self-signed certificate
    cosmosOptions.HttpClientFactory(() => { /* ... */ });
}
else
{
    // Production: Use configured connection mode
    cosmosOptions.ConnectionMode(
        _options.ConnectionMode == "Direct"
            ? Microsoft.Azure.Cosmos.ConnectionMode.Direct
            : Microsoft.Azure.Cosmos.ConnectionMode.Gateway
    );

    // Only set Direct mode settings when actually using Direct mode
    if (_options.ConnectionMode == "Direct")
    {
        cosmosOptions.MaxRequestsPerTcpConnection(_options.MaxConcurrentConnections ?? 16);
        cosmosOptions.MaxTcpConnectionsPerEndpoint(_options.MaxConcurrentConnections ?? 16);
    }
}
```

---

## Files Fixed

### 1. **CosmosDbExtensions.cs**

**Method:** `ConfigureDbContext()`

**Changes:**
- ? Prioritize emulator configuration (check `UseEmulator` first)
- ? Only set Direct mode settings when **not using emulator** and **ConnectionMode is Direct**
- ? Removed duplicate connection mode setting

**Before:**
```csharp
cosmosOptions.ConnectionMode(options.ConnectionMode == "Direct" ? Direct : Gateway);
cosmosOptions.MaxRequestsPerTcpConnection(options.MaxConcurrentConnections ?? 16);
cosmosOptions.MaxTcpConnectionsPerEndpoint(options.MaxConcurrentConnections ?? 16);

if (options.UseEmulator)
{
    cosmosOptions.ConnectionMode(Gateway); // ? Override creates conflict
}
```

**After:**
```csharp
if (options.UseEmulator)
{
    cosmosOptions.ConnectionMode(Gateway); // ? Set first
}
else
{
    cosmosOptions.ConnectionMode(options.ConnectionMode == "Direct" ? Direct : Gateway);
    
    if (options.ConnectionMode == "Direct") // ? Conditional
    {
        cosmosOptions.MaxRequestsPerTcpConnection(options.MaxConcurrentConnections ?? 16);
        cosmosOptions.MaxTcpConnectionsPerEndpoint(options.MaxConcurrentConnections ?? 16);
    }
}
```

### 2. **OrderDbContext.cs**

**Method:** `OnConfiguring()`

**Changes:** Same pattern as `CosmosDbExtensions.cs`

### 3. **CosmosDbInitializer.cs**

**Constructor:** `CosmosDbInitializer()`

**Changes:**
- ? Removed initial connection mode setting
- ? Set connection mode **only once** based on emulator flag

**Before:**
```csharp
var clientOptions = new CosmosClientOptions
{
    ConnectionMode = Direct, // ? Set here
    // ...
};

if (_options.UseEmulator)
{
    clientOptions.ConnectionMode = Gateway; // ? Override creates conflict
}
```

**After:**
```csharp
var clientOptions = new CosmosClientOptions
{
    // ? Connection mode NOT set here
    // ...
};

if (_options.UseEmulator)
{
    clientOptions.ConnectionMode = Gateway; // ? Set once
}
else
{
    clientOptions.ConnectionMode = _options.ConnectionMode == "Direct" ? Direct : Gateway; // ? Set once
}
```

---

## Configuration Matrix

| Scenario | UseEmulator | ConnectionMode | MaxTcp Settings | Result |
|----------|-------------|----------------|-----------------|--------|
| **Emulator (Dev)** | `true` | Gateway (forced) | ? Not set | ? Works |
| **Production (Gateway)** | `false` | Gateway | ? Not set | ? Works |
| **Production (Direct)** | `false` | Direct | ? Set | ? Works |
| **Old Code (Emulator)** | `true` | Gateway (forced) | ? Set | ? **Exception** |

---

## Why Gateway Mode for Emulator?

### Reasons for Gateway Mode:

1. **SSL Certificate Bypass Compatibility**
   - The `HttpClientFactory` SSL bypass works best with Gateway mode
   - Direct mode has additional TCP connection complexities

2. **Emulator Limitations**
   - Emulator doesn't fully support Direct mode optimizations
   - Gateway mode is more stable for local development

3. **Simplified Configuration**
   - No need to configure TCP connection pools
   - Fewer moving parts for local testing

4. **Microsoft Recommendation**
   - [Official documentation](https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator) recommends Gateway mode for emulator

---

## Testing

### Test Configuration

**appsettings.Development.json:**
```json
{
  "CosmosDB": {
    "UseEmulator": true,
    "AccountEndpoint": "https://localhost:8081",
    "AccountKey": "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
    "ConnectionMode": "Direct"  // ? Ignored when UseEmulator=true
  }
}
```

**Result:** Uses Gateway mode (no exception)

**appsettings.Production.json:**
```json
{
  "CosmosDB": {
    "UseEmulator": false,
    "AccountEndpoint": "https://your-account.documents.azure.com:443/",
    "AccountKey": "your-production-key",
    "ConnectionMode": "Direct"  // ? Used with MaxTcp settings
  }
}
```

**Result:** Uses Direct mode with connection pooling (optimized)

---

## Verification

### Test Emulator Connection

```csharp
[Fact]
public async Task InitializeAsync_ShouldConnectToEmulator()
{
    // Arrange: UseEmulator = true in config
    var initializer = _testHost.Services.GetRequiredService<CosmosDbInitializer>();
    
    // Act
    var canConnect = await initializer.CanConnectAsync();
    
    // Assert
    canConnect.Should().BeTrue(); // ? No ArgumentException
}
```

### Run Tests

```cmd
dotnet test --filter "FullyQualifiedName~CosmosDbOrderTests"
```

**Expected:** All tests pass without `ArgumentException`

---

## Performance Impact

### Emulator (Development)

| Aspect | Gateway Mode | Direct Mode |
|--------|--------------|-------------|
| **Latency** | ~5-10ms | ~3-5ms |
| **Connection Overhead** | Higher | Lower |
| **Impact** | Minimal (local network) | Not significant |
| **Stability** | ? High | ?? Variable |

**Verdict:** Gateway mode is acceptable for emulator (development only)

### Production

| Scenario | Mode | Configuration |
|----------|------|---------------|
| **High Throughput** | Direct | ? MaxTcp settings enabled |
| **Firewall Restrictions** | Gateway | ? MaxTcp settings disabled |
| **Default** | Direct | ? MaxTcp settings enabled |

---

## Summary of Changes

### Before (Broken)

```csharp
// ? PROBLEM: Set Direct settings, then override to Gateway
cosmosOptions.ConnectionMode(Direct);
cosmosOptions.MaxTcpConnectionsPerEndpoint(16); // ? Requires Direct mode

if (options.UseEmulator)
{
    cosmosOptions.ConnectionMode(Gateway); // ? Override causes exception
}
```

### After (Fixed)

```csharp
// ? SOLUTION: Check emulator first, conditionally set Direct settings
if (options.UseEmulator)
{
    cosmosOptions.ConnectionMode(Gateway); // ? Set once for emulator
}
else
{
    cosmosOptions.ConnectionMode(options.ConnectionMode == "Direct" ? Direct : Gateway);
    
    if (options.ConnectionMode == "Direct") // ? Only set if Direct
    {
        cosmosOptions.MaxTcpConnectionsPerEndpoint(16);
    }
}
```

---

## Best Practices

### ? Do

1. **Check emulator flag first** before setting connection mode
2. **Set connection mode only once** (no overrides)
3. **Conditionally set Direct mode settings** (only when mode is Direct)
4. **Use Gateway mode for emulator** (simpler, more stable)
5. **Use Direct mode for production** (better performance)

### ? Don't

1. ? Set connection mode, then override it
2. ? Set `MaxTcpConnectionsPerEndpoint` when using Gateway mode
3. ? Mix emulator and production configuration
4. ? Use Direct mode with emulator (unnecessary complexity)

---

## Additional Resources

- [Cosmos DB Connection Modes](https://learn.microsoft.com/en-us/azure/cosmos-db/sql/sdk-connection-modes)
- [CosmosClientOptions API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos.cosmosclientoptions)
- [Emulator Best Practices](https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator-release-notes)

---

**Fixed:** December 2024  
**Framework:** .NET 8  
**SDK Version:** Microsoft.Azure.Cosmos 3.42+  

? **ArgumentException resolved - Connection mode configuration is now correct!**
