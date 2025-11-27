# Cosmos DB EF Core Provider Limitations

## Issue: CanConnectAsync Not Supported

### Error
```
System.NotSupportedException: The Cosmos database does not support 'CanConnect' or 'CanConnectAsync'.
```

### Root Cause

The EF Core Cosmos provider **does not support** these methods:
- `DbContext.Database.CanConnect()`
- `DbContext.Database.CanConnectAsync()`

This is a known limitation of the Cosmos DB provider.

**Reference:** [EF Core Cosmos Limitations](https://learn.microsoft.com/en-us/ef/core/providers/cosmos/limitations)

---

## Solution

### ? Don't Use (Not Supported)

```csharp
// This will throw NotSupportedException
var canConnect = await _context.Database.CanConnectAsync();
```

### ? Use Instead

```csharp
// Use CosmosDbInitializer which implements proper connection check
var initializer = _testHost.Services.GetRequiredService<CosmosDbInitializer>();
var canConnect = await initializer.CanConnectAsync();
```

---

## How CosmosDbInitializer Works

### Implementation (CosmosDbInitializer.cs)

```csharp
public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
{
    try
    {
        // Use CosmosClient directly to list databases
        var databases = _cosmosClient.GetDatabaseQueryIterator<DatabaseProperties>();
        if (databases.HasMoreResults)
        {
            await databases.ReadNextAsync(cancellationToken);
        }
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Cannot connect to Cosmos DB at {Endpoint}", _options.GetEndpoint());
        return false;
    }
}
```

**Why it works:**
- Uses `CosmosClient` directly (not EF Core)
- Performs actual operation (list databases)
- Handles exceptions gracefully
- Returns true/false based on success

---

## Fixed Test

### Before (Broken)

```csharp
[Fact]
public async Task DatabaseConnection_ShouldBeConfigured()
{
    // Act
    var canConnect = await _context.Database.CanConnectAsync(); // ? NotSupportedException
    
    // Assert
    canConnect.Should().BeTrue();
}
```

### After (Fixed)

```csharp
[Fact]
public async Task DatabaseConnection_ShouldBeConfigured()
{
    // Act - Use CosmosDbInitializer instead of DbContext.CanConnectAsync
    var initializer = _testHost.Services.GetRequiredService<CosmosDbInitializer>();
    var canConnect = await initializer.CanConnectAsync(); // ? Works!
    
    // Assert
    canConnect.Should().BeTrue();
    _output.WriteLine("? Database connection verified");
}
```

---

## Other EF Core Cosmos Limitations

### Not Supported Operations

| Operation | Status | Alternative |
|-----------|--------|-------------|
| `CanConnect()` | ? Not Supported | Use `CosmosDbInitializer.CanConnectAsync()` |
| `CanConnectAsync()` | ? Not Supported | Use `CosmosDbInitializer.CanConnectAsync()` |
| `EnsureDeleted()` | ? Not Supported | Use `CosmosClient` directly |
| `EnsureDeletedAsync()` | ? Not Supported | Use `CosmosClient` directly |
| `Migrate()` | ? Not Supported | Use `EnsureCreated()` instead |
| `MigrateAsync()` | ? Not Supported | Use `EnsureCreatedAsync()` instead |

### Supported Operations

| Operation | Status | Usage |
|-----------|--------|-------|
| `EnsureCreated()` | ? Supported | Create database/container |
| `EnsureCreatedAsync()` | ? Supported | Create database/container (async) |
| `SaveChanges()` | ? Supported | Standard EF Core |
| `SaveChangesAsync()` | ? Supported | Standard EF Core |
| `Add()` / `Remove()` | ? Supported | Standard EF Core |
| LINQ Queries | ? Supported | Most queries work |

---

## Best Practices

### ? Do

1. **Use CosmosDbInitializer for connection checks**
   ```csharp
   var initializer = services.GetRequiredService<CosmosDbInitializer>();
   var canConnect = await initializer.CanConnectAsync();
   ```

2. **Use CosmosClient for database management**
   ```csharp
   var database = cosmosClient.GetDatabase(databaseName);
   await database.DeleteAsync();
   ```

3. **Use EF Core for CRUD operations**
   ```csharp
   _context.Orders.Add(order);
   await _context.SaveChangesAsync();
   ```

### ? Don't

1. ? Don't use `CanConnectAsync()` on DbContext
2. ? Don't use `EnsureDeletedAsync()` on DbContext
3. ? Don't use migrations (not supported)
4. ? Don't rely on transactions (limited support)

---

## Testing Connection

### Recommended Approach

```csharp
[Fact]
public async Task ShouldConnectToCosmosDb()
{
    // Arrange
    var initializer = _testHost.Services.GetRequiredService<CosmosDbInitializer>();
    
    // Act
    var canConnect = await initializer.CanConnectAsync();
    
    // Assert
    canConnect.Should().BeTrue();
    
    // Verify database exists
    var info = await initializer.GetDatabaseInfoAsync();
    info.DatabaseExists.Should().BeTrue();
}
```

### Alternative: Try Actual Operation

```csharp
[Fact]
public async Task ShouldConnectByQueryingData()
{
    // Arrange & Act
    var orderExists = await _context.Orders
        .WithPartitionKey("TEST")
        .AnyAsync();
    
    // Assert - If we got here, connection works
    orderExists.Should().BeFalse(); // No data yet
}
```

---

## Summary

| Aspect | Details |
|--------|---------|
| **Error** | `NotSupportedException: CanConnectAsync not supported` |
| **Cause** | EF Core Cosmos provider limitation |
| **Fix** | Use `CosmosDbInitializer.CanConnectAsync()` |
| **File Changed** | `CosmosDbContextTests.cs` |
| **Line** | Line 526 |
| **Status** | ? Fixed |

---

## Related Documentation

- [EF Core Cosmos Provider Limitations](https://learn.microsoft.com/en-us/ef/core/providers/cosmos/limitations)
- [Cosmos DB SDK Documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos)
- [CosmosClient API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos.cosmosclient)

---

**Fixed:** December 2024  
**Framework:** .NET 8  
**EF Core Version:** 8.0.11  
**Provider:** Microsoft.EntityFrameworkCore.Cosmos

? **NotSupportedException resolved - Use CosmosDbInitializer for connection checks!**
