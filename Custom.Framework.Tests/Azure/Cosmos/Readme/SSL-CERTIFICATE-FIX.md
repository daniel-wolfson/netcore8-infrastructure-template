# SSL Certificate Fix for Cosmos DB Emulator

## Problem

When connecting to Cosmos DB Emulator, you may encounter this error:

```
InnerException = {"The remote certificate is invalid because of errors in the certificate chain: UntrustedRoot"}
```

This occurs because the Cosmos DB Emulator uses a self-signed SSL certificate that is not trusted by default.

---

## Solution

The fix has been applied to three key files:

### 1. **CosmosDbInitializer.cs**

Added SSL certificate bypass for emulator in the constructor:

```csharp
// Handle SSL certificate validation for Cosmos DB Emulator
if (_options.UseEmulator)
{
    _logger.LogWarning("Using Cosmos DB Emulator - bypassing SSL certificate validation");
    
    // Configure HttpClientFactory to bypass SSL validation for emulator
    clientOptions.HttpClientFactory = () =>
    {
        HttpMessageHandler httpMessageHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (req, cert, chain, errors) =>
            {
                // Allow self-signed certificates for emulator
                return true;
            }
        };

        return new HttpClient(httpMessageHandler);
    };
    
    // Use Gateway mode for better emulator compatibility
    clientOptions.ConnectionMode = Microsoft.Azure.Cosmos.ConnectionMode.Gateway;
}
```

### 2. **OrderDbContext.cs**

Added SSL certificate handling in `OnConfiguring`:

```csharp
// Configure SSL validation for emulator
if (_options.UseEmulator)
{
    cosmosOptions.HttpClientFactory(() =>
    {
        HttpMessageHandler httpMessageHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (req, cert, chain, errors) => true
        };
        return new HttpClient(httpMessageHandler);
    });
    
    // Override to Gateway mode for emulator
    cosmosOptions.ConnectionMode(Microsoft.Azure.Cosmos.ConnectionMode.Gateway);
}
```

### 3. **CosmosDbExtensions.cs**

Added SSL handling in `ConfigureDbContext`:

```csharp
// Handle SSL certificate validation for Cosmos DB Emulator
if (options.UseEmulator)
{
    cosmosOptions.HttpClientFactory(() =>
    {
        HttpMessageHandler httpMessageHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (req, cert, chain, errors) => true
        };
        return new HttpClient(httpMessageHandler);
    });
    
    // Use Gateway mode for better emulator compatibility
    cosmosOptions.ConnectionMode(Microsoft.Azure.Cosmos.ConnectionMode.Gateway);
}
```

---

## How It Works

### When `UseEmulator = true`:

1. **Bypasses SSL Certificate Validation**
   - Custom `HttpClientHandler` with `ServerCertificateCustomValidationCallback` that returns `true`
   - Allows self-signed certificates from the emulator

2. **Switches to Gateway Mode**
   - More compatible with emulator
   - Avoids Direct mode connection issues

3. **Only Applied for Emulator**
   - Controlled by `UseEmulator` flag in configuration
   - Production connections remain secure

---

## Configuration

### appsettings.json (Emulator)

```json
{
  "CosmosDB": {
    "UseEmulator": true,
    "AccountEndpoint": "https://localhost:8081",
    "AccountKey": "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
  }
}
```

**Key setting:** `"UseEmulator": true` enables SSL bypass

### appsettings.json (Production)

```json
{
  "CosmosDB": {
    "UseEmulator": false,
    "AccountEndpoint": "https://your-account.documents.azure.com:443/",
    "AccountKey": "your-production-key"
  }
}
```

**Key setting:** `"UseEmulator": false` disables SSL bypass (secure production connection)

---

## Security Considerations

### ?? Emulator Only

The SSL bypass is **ONLY** applied when:
- `UseEmulator = true` in configuration
- Running against local emulator (localhost:8081)

### ? Production Security

Production connections (`UseEmulator = false`) maintain full SSL validation:
- Certificate chain validation
- Certificate trust verification
- No certificate bypasses

### ?? Best Practices

1. **Never set `UseEmulator = true` in production**
2. **Use environment-specific configuration files**
   - `appsettings.Development.json` ? `UseEmulator = true`
   - `appsettings.Production.json` ? `UseEmulator = false`
3. **Store production keys in Azure Key Vault**
4. **Use managed identities in Azure**

---

## Testing the Fix

### 1. Start Cosmos DB Emulator

```cmd
cosmos-start.bat
```

Or manually:
```cmd
docker-compose -f docker-compose.cosmos.yml up -d
```

### 2. Run Tests

```cmd
dotnet test --filter "FullyQualifiedName~CosmosDbOrderTests.CanConnect_ShouldReturnTrue"
```

**Expected:** Test passes without SSL certificate errors

### 3. Verify Connection

```csharp
var initializer = services.GetRequiredService<CosmosDbInitializer>();
var canConnect = await initializer.CanConnectAsync();

Console.WriteLine($"Can connect: {canConnect}"); // Should be: true
```

---

## Troubleshooting

### Issue: Still Getting SSL Error

**Check:**
1. ? `UseEmulator = true` in configuration
2. ? Emulator is running: `docker ps | findstr cosmos`
3. ? Using correct endpoint: `https://localhost:8081`

**Verify:**
```cmd
# Check emulator accessibility
curl -k https://localhost:8081/_explorer/emulator.pem
```

### Issue: Tests Pass but Browser Doesn't Work

**Browser requires manual certificate trust:**

1. **Download certificate:**
   ```cmd
   curl -k https://localhost:8081/_explorer/emulator.pem > emulator.pem
   ```

2. **Import to Windows (as Administrator):**
   ```powershell
   Import-Certificate -FilePath .\emulator.pem -CertStoreLocation Cert:\CurrentUser\Root
   ```

3. **Or accept browser warning:**
   - Navigate to: https://localhost:8081/_explorer/index.html
   - Click "Advanced" ? "Proceed to localhost"

### Issue: Production Connection Fails

**This fix only affects emulator connections.**

If production fails:
1. ? Verify `UseEmulator = false`
2. ? Check endpoint and key are correct
3. ? Verify firewall allows outbound HTTPS
4. ? Check Azure Cosmos DB account is accessible

---

## Alternative: Trust Certificate System-Wide (Windows)

If you prefer to trust the emulator certificate system-wide:

### Option 1: PowerShell (Recommended)

```powershell
# Run as Administrator
$cert = Invoke-WebRequest -Uri https://localhost:8081/_explorer/emulator.pem -SkipCertificateCheck
$cert.Content | Out-File emulator.pem
Import-Certificate -FilePath .\emulator.pem -CertStoreLocation Cert:\CurrentUser\Root
```

### Option 2: Manual

1. Open browser: https://localhost:8081/_explorer/index.html
2. Click padlock ? "Connection is not secure"
3. Click "More information" ? "View Certificate"
4. Click "Details" ? "Export"
5. Save as `emulator.cer`
6. Double-click `emulator.cer`
7. Click "Install Certificate"
8. Choose "Current User"
9. Select "Place all certificates in the following store"
10. Choose "Trusted Root Certification Authorities"
11. Click "Finish"

**After trusting:** Remove `UseEmulator = true` and code will use standard SSL validation

---

## Code Changes Summary

| File | Change | Purpose |
|------|--------|---------|
| `CosmosDbInitializer.cs` | Added SSL bypass in constructor | Direct SDK operations |
| `OrderDbContext.cs` | Added SSL bypass in OnConfiguring | EF Core Cosmos provider |
| `CosmosDbExtensions.cs` | Added SSL bypass in ConfigureDbContext | Service registration |

**All changes:**
- ? Conditional (only when `UseEmulator = true`)
- ? Logged (warning message when bypassing SSL)
- ? Safe (production connections unaffected)
- ? Standard (uses recommended Gateway mode for emulator)

---

## Related Documentation

- [Cosmos DB Emulator Docs](https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator)
- [EF Core Cosmos Provider](https://learn.microsoft.com/en-us/ef/core/providers/cosmos/)
- [SSL Certificate Troubleshooting](https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator#troubleshoot-issues)

---

## Quick Reference

### Enable SSL Bypass (Development)

```json
{
  "CosmosDB": {
    "UseEmulator": true
  }
}
```

### Disable SSL Bypass (Production)

```json
{
  "CosmosDB": {
    "UseEmulator": false
  }
}
```

### Test Connection

```cmd
dotnet test --filter "FullyQualifiedName~CosmosDbOrderTests.CanConnect_ShouldReturnTrue"
```

---

**Fixed:** December 2024  
**Framework:** .NET 8  
**SDK Version:** Microsoft.Azure.Cosmos 3.42+  
**EF Core Version:** 8.0.11

? **SSL certificate errors are now resolved for Cosmos DB Emulator!**
