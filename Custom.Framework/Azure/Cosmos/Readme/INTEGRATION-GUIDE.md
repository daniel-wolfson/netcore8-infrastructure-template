# Azure Cosmos DB Integration for Hospitality Order Management

## Overview

This integration provides Azure Cosmos DB support for managing temporary order data in the hospitality industry reservation flow. It uses **Entity Framework Core Cosmos provider** to store order context data with automatic TTL (Time-To-Live) management based on order status.

### Key Features

- ? **Automatic TTL Management**: Orders are automatically deleted based on their status
  - Pending orders: 10 minutes (configurable)
  - Succeeded orders: 7 days (configurable)
  - Failed/Cancelled orders: 10 minutes (configurable)
- ? **Partition Key Strategy**: Uses hotel code for efficient querying and scalability
- ? **Entity Framework Core**: Full EF Core support with LINQ queries
- ? **Optimistic Concurrency**: ETag-based concurrency control
- ? **Local Development**: Cosmos DB Emulator support
- ? **Production Ready**: Autoscale throughput, retry policies, connection pooling

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Installation](#installation)
3. [Configuration](#configuration)
4. [Usage Examples](#usage-examples)
5. [Order Flow Integration](#order-flow-integration)
6. [Repository Methods](#repository-methods)
7. [TTL Management](#ttl-management)
8. [Performance Optimization](#performance-optimization)
9. [Local Development](#local-development)
10. [Production Deployment](#production-deployment)
11. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required NuGet Packages

Add these packages to your `Custom.Framework.csproj`:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Cosmos" Version="8.0.*" />
<PackageReference Include="Microsoft.Azure.Cosmos" Version="3.42.*" />
```

### Install via .NET CLI

```bash
cd Custom.Framework
dotnet add package Microsoft.EntityFrameworkCore.Cosmos
dotnet add package Microsoft.Azure.Cosmos
```

---

## Installation

### Step 1: Install Cosmos DB Emulator (Local Development)

**Windows:**
Download and install from: https://aka.ms/cosmosdb-emulator

**Linux/macOS (Docker):**

**Option 1: Using Docker Compose (Recommended)**
```bash
cd Custom.Framework.Tests/Azure
./cosmos-start.bat         # Windows
.\cosmos.ps1 start         # PowerShell
docker-compose -f docker-compose.cosmos.yml up -d  # Linux/macOS
```

See `Custom.Framework.Tests/Azure/DOCKER-COSMOS-README.md` for complete Docker setup guide.

**Option 2: Manual Docker Run**
```bash
docker run -p 8081:8081 -p 10251:10251 -p 10252:10252 -p 10253:10253 -p 10254:10254 \
  -e AZURE_COSMOS_EMULATOR_PARTITION_COUNT=10 \
  -e AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE=true \
  --name=cosmos-emulator \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
```

### Step 2: Configure Application Settings

Add to your `appsettings.json` or use the provided `appsettings.cosmos.json`:

```json
{
  "CosmosDB": {
    "UseEmulator": true,
    "AccountEndpoint": "https://your-cosmos-account.documents.azure.com:443/",
    "AccountKey": "your-account-key-here",
    "DatabaseName": "HospitalityOrders",
    "ContainerName": "Orders",
    "PartitionKeyPath": "/hotelCode",
    "DefaultTtlSeconds": 600,
    "SucceededTtlSeconds": 604800,
    "MaxThroughput": 4000,
    "EnableAutoscale": true
  }
}
```

### Step 3: Register Services in Program.cs

```csharp
using Custom.Framework.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// Add Cosmos DB services
builder.Services.AddCosmosDbForOrders(builder.Configuration);

var app = builder.Build();

// Initialize Cosmos DB (create database/container if not exists)
app.UseCosmosDb();

app.Run();
```

**For Test Projects (IHost):**

```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddCosmosDbForOrders(context.Configuration);
    })
    .Build();

// Initialize Cosmos DB
await host.UseCosmosDbAsync();

await host.RunAsync();
```

---

## Configuration

### Configuration Options

| Property | Description | Default |
|----------|-------------|---------|
| `UseEmulator` | Use Cosmos DB Emulator for local development | `false` |
| `AccountEndpoint` | Cosmos DB account endpoint URL | - |
| `AccountKey` | Cosmos DB account key | - |
| `DatabaseName` | Database name | `HospitalityOrders` |
| `ContainerName` | Container name for orders | `Orders` |
| `PartitionKeyPath` | Partition key path | `/hotelCode` |
| `DefaultTtlSeconds` | TTL for pending orders (seconds) | `600` (10 min) |
| `SucceededTtlSeconds` | TTL for succeeded orders (seconds) | `604800` (7 days) |
| `MaxThroughput` | Maximum RU/s (autoscale) | `4000` |
| `EnableAutoscale` | Enable autoscale throughput | `true` |
| `ConnectionMode` | Connection mode: Direct or Gateway | `Direct` |
| `ApplicationRegion` | Preferred region for operations | `null` |
| `AllowBulkExecution` | Enable bulk operations | `true` |
| `EnableDetailedLogging` | Enable detailed logging | `false` |

### Environment-Specific Configuration

**appsettings.Development.json** (Emulator):
```json
{
  "CosmosDB": {
    "UseEmulator": true,
    "DefaultTtlSeconds": 300,
    "EnableDetailedLogging": true
  }
}
```

**appsettings.Production.json** (Azure):
```json
{
  "CosmosDB": {
    "UseEmulator": false,
    "AccountEndpoint": "https://your-account.documents.azure.com:443/",
    "AccountKey": "your-production-key",
    "ApplicationRegion": "East US",
    "MaxThroughput": 10000,
    "EnableAutoscale": true
  }
}
```

---

## Usage Examples

### Basic Order Operations

#### 1. Create a New Order

```csharp
using Custom.Framework.Azure.Cosmos;
using Custom.Framework.Azure.Cosmos.Models;

public class ReservationService
{
    private readonly IOrderRepository _orderRepository;

    public ReservationService(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<OrderContext> CreateNewOrder(string hotelCode, string sessionId)
    {
        var order = new OrderContext
        {
            HotelCode = hotelCode,
            SessionId = sessionId,
            Status = OrderStatus.Pending,
            CurrentStep = "SearchHeader",
            OrderData = new OrderData
            {
                Adults = 2,
                Children = 0,
                Infants = 0,
                SearchCriteria = new Dictionary<string, object>
                {
                    ["destination"] = hotelCode,
                    ["searchDate"] = DateTime.UtcNow
                }
            }
        };

        return await _orderRepository.CreateOrderAsync(order);
    }
}
```

#### 2. Update Order as User Progresses

```csharp
public async Task UpdateOrderStep(string orderId, string hotelCode, string step, OrderData data)
{
    var order = await _orderRepository.GetOrderByIdAsync(orderId, hotelCode);
    if (order == null)
        throw new InvalidOperationException("Order not found");

    order.CurrentStep = step;
    order.OrderData = data;

    await _orderRepository.UpdateOrderAsync(order);
}
```

#### 3. Complete Order After Payment

```csharp
public async Task CompleteOrder(string orderId, string hotelCode, PaymentInfo paymentInfo)
{
    var order = await _orderRepository.GetOrderByIdAsync(orderId, hotelCode);
    if (order == null)
        throw new InvalidOperationException("Order not found");

    order.Status = OrderStatus.Succeeded;
    order.CurrentStep = "ReservationSummary";
    order.PaymentInfo = paymentInfo;

    // This will automatically set TTL to 7 days (SucceededTtlSeconds)
    await _orderRepository.UpdateOrderAsync(order);
}
```

---

## Order Flow Integration

### Hospitality Reservation Flow Steps

```
1. SearchHeader ? 2. SelectHeader ? 3. ReservationHeader ? 
4. EdgeServiceVerification ? 5. Payment ? 6. PaymentResult ? 7. ReservationSummary
```

### Flow Implementation

```csharp
public class OrderFlowService
{
    private readonly IOrderRepository _orderRepository;

    public OrderFlowService(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    // Step 1: Search Header
    public async Task<OrderContext> StartSearchAsync(string hotelCode, string sessionId, 
        DateTime checkIn, DateTime checkOut, int adults, int children)
    {
        var order = new OrderContext
        {
            HotelCode = hotelCode,
            SessionId = sessionId,
            Status = OrderStatus.Pending,
            CurrentStep = "SearchHeader",
            OrderData = new OrderData
            {
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                Adults = adults,
                Children = children
            }
        };

        return await _orderRepository.CreateOrderAsync(order);
    }

    // Step 2: Select Header (Room + Plan selection)
    public async Task<OrderContext> SelectHeaderAsync(string orderId, string hotelCode,
        string roomCode, string planCode, string priceCode, decimal totalAmount)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId, hotelCode);
        if (order == null)
            throw new InvalidOperationException("Order not found");

        order.CurrentStep = "SelectHeader";
        order.OrderData.RoomCode = roomCode;
        order.OrderData.PlanCode = planCode;
        order.OrderData.PriceCode = priceCode;
        order.OrderData.TotalAmount = totalAmount;

        return await _orderRepository.UpdateOrderAsync(order);
    }

    // Step 3: Reservation Header (Guest details)
    public async Task<OrderContext> AddGuestDetailsAsync(string orderId, string hotelCode,
        GuestInfo guestInfo)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId, hotelCode);
        if (order == null)
            throw new InvalidOperationException("Order not found");

        order.CurrentStep = "ReservationHeader";
        order.OrderData.GuestInfo = guestInfo;

        return await _orderRepository.UpdateOrderAsync(order);
    }

    // Step 4: Edge Service Verification
    public async Task<OrderContext> VerifyOrderAsync(string orderId, string hotelCode,
        VerificationResult verificationResult)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId, hotelCode);
        if (order == null)
            throw new InvalidOperationException("Order not found");

        order.CurrentStep = "EdgeServiceVerification";
        order.VerificationResult = verificationResult;

        if (verificationResult.IsVerified)
        {
            order.Status = OrderStatus.PaymentInProgress;
        }

        return await _orderRepository.UpdateOrderAsync(order);
    }

    // Step 5: Payment Processing
    public async Task<OrderContext> ProcessPaymentAsync(string orderId, string hotelCode,
        PaymentInfo paymentInfo)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId, hotelCode);
        if (order == null)
            throw new InvalidOperationException("Order not found");

        order.CurrentStep = "Payment";
        order.PaymentInfo = paymentInfo;

        if (paymentInfo.PaymentStatus == "Success")
        {
            order.Status = OrderStatus.Succeeded;
        }
        else
        {
            order.Status = OrderStatus.Failed;
        }

        return await _orderRepository.UpdateOrderAsync(order);
    }

    // Step 6: Retrieve order by session for current user
    public async Task<OrderContext?> GetCurrentOrderAsync(string sessionId, string hotelCode)
    {
        return await _orderRepository.GetOrderBySessionIdAsync(sessionId, hotelCode);
    }
}
```

---

## Repository Methods

### IOrderRepository Interface

| Method | Description |
|--------|-------------|
| `CreateOrderAsync` | Create a new order |
| `GetOrderByIdAsync` | Get order by ID and hotel code |
| `GetOrderBySessionIdAsync` | Get order by session ID |
| `UpdateOrderAsync` | Update existing order |
| `UpdateOrderStatusAsync` | Update order status only |
| `UpdateOrderStepAsync` | Update current step only |
| `DeleteOrderAsync` | Delete order |
| `GetOrdersByHotelAsync` | Get all orders for a hotel |
| `GetOrdersByStatusAsync` | Get orders by status |
| `GetPendingOrdersAsync` | Get pending orders |
| `GetExpiringOrdersAsync` | Get orders about to expire |
| `OrderExistsAsync` | Check if order exists |

### Query Examples

```csharp
// Get all pending orders for a hotel
var pendingOrders = await _orderRepository.GetPendingOrdersAsync("HOTEL001");

// Get all succeeded orders
var succeededOrders = await _orderRepository.GetOrdersByStatusAsync("HOTEL001", OrderStatus.Succeeded);

// Get orders expiring in 5 minutes
var expiringOrders = await _orderRepository.GetExpiringOrdersAsync("HOTEL001", withinMinutes: 5);

// Check if order exists
var exists = await _orderRepository.OrderExistsAsync(orderId, hotelCode);
```

---

## TTL Management

### Automatic TTL by Status

The repository automatically sets TTL based on order status:

| Order Status | TTL | Description |
|--------------|-----|-------------|
| `Pending` | 10 minutes (600s) | Default for incomplete orders |
| `PaymentInProgress` | 10 minutes (600s) | During payment |
| `Succeeded` | 7 days (604800s) | Completed orders kept longer |
| `Failed` | 10 minutes (600s) | Failed orders cleaned up quickly |
| `Cancelled` | 10 minutes (600s) | Cancelled orders cleaned up quickly |

### Custom TTL

```csharp
var order = new OrderContext
{
    HotelCode = "HOTEL001",
    SessionId = sessionId,
    Ttl = 1800 // Custom 30 minutes TTL
};

await _orderRepository.CreateOrderAsync(order);
```

### Disable TTL (Never Expire)

```csharp
order.Ttl = -1; // Document never expires
await _orderRepository.UpdateOrderAsync(order);
```

---

## Performance Optimization

### 1. Partition Key Strategy

Orders are partitioned by `hotelCode` for:
- ? Efficient querying within a hotel
- ? Scalability across multiple hotels
- ? Cost optimization (queries within partition)

### 2. Composite Indexes

Automatically configured composite indexes:
- `hotelCode + status`
- `hotelCode + sessionId`
- `hotelCode + createdAt`

### 3. Autoscale Throughput

```json
{
  "CosmosDB": {
    "EnableAutoscale": true,
    "MaxThroughput": 4000
  }
}
```

Autoscale benefits:
- Automatically scales RU/s based on load
- Cost-effective (only pay for what you use)
- No manual intervention needed

### 4. Connection Pooling

```csharp
builder.Services.AddCosmosDbForOrders(builder.Configuration, options =>
{
    options.MaxConcurrentConnections = 50;
    options.AllowBulkExecution = true;
});
```

### 5. Query Optimization

**? Bad (cross-partition query):**
```csharp
// Expensive - scans all partitions
var orders = await _context.Orders
    .Where(o => o.SessionId == sessionId)
    .ToListAsync();
```

**? Good (partition-scoped query):**
```csharp
// Efficient - single partition query
var orders = await _context.Orders
    .WithPartitionKey(hotelCode)
    .Where(o => o.SessionId == sessionId)
    .ToListAsync();
```

---

## Local Development

### Using Cosmos DB Emulator

#### 1. Start Emulator

**Windows:**
- Launch "Azure Cosmos DB Emulator" from Start menu
- Access at: https://localhost:8081/_explorer/index.html

**Docker:**
```bash
docker run -p 8081:8081 -p 10251-10254:10251-10254 \
  --name cosmos-emulator \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
```

#### 2. Configure App to Use Emulator

```json
{
  "CosmosDB": {
    "UseEmulator": true
  }
}
```

#### 3. Trust Emulator Certificate (First Time)

**Windows:**
```powershell
# Export certificate from emulator
Invoke-WebRequest -Uri https://localhost:8081/_explorer/emulator.pem -OutFile emulator.pem

# Import to trusted root
Import-Certificate -FilePath .\emulator.pem -CertStoreLocation Cert:\CurrentUser\Root
```

**Linux/macOS:**
```bash
curl -k https://localhost:8081/_explorer/emulator.pem > emulatorcert.crt
sudo cp emulatorcert.crt /usr/local/share/ca-certificates/
sudo update-ca-certificates
```

---

## Production Deployment

### Azure Cosmos DB Setup

#### 1. Create Cosmos DB Account

```bash
# Azure CLI
az cosmosdb create \
  --name your-cosmos-account \
  --resource-group your-rg \
  --default-consistency-level Session \
  --locations regionName='East US' failoverPriority=0 isZoneRedundant=False

# Get connection details
az cosmosdb keys list \
  --name your-cosmos-account \
  --resource-group your-rg \
  --type keys
```

#### 2. Configure Production Settings

```json
{
  "CosmosDB": {
    "UseEmulator": false,
    "AccountEndpoint": "https://your-cosmos-account.documents.azure.com:443/",
    "AccountKey": "your-production-key",
    "DatabaseName": "HospitalityOrders",
    "ApplicationRegion": "East US",
    "MaxThroughput": 10000,
    "EnableAutoscale": true,
    "ConnectionMode": "Direct"
  }
}
```

#### 3. Use Azure Key Vault (Recommended)

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://your-keyvault.vault.azure.net/"),
    new DefaultAzureCredential());

builder.Services.AddCosmosDbForOrders(builder.Configuration, options =>
{
    // Key Vault stores: CosmosDB--AccountKey
    options.AccountKey = builder.Configuration["CosmosDB:AccountKey"];
});
```

#### 4. Enable Monitoring

```csharp
builder.Services.AddApplicationInsightsTelemetry();
```

---

## Troubleshooting

### Common Issues

#### Issue 1: Cannot Connect to Emulator

**Error:** `The SSL connection could not be established`

**Solution:**
```powershell
# Trust emulator certificate
curl -k https://localhost:8081/_explorer/emulator.pem > emulatorcert.crt
# Import certificate to trusted root
```

#### Issue 2: Throughput Exceeded (429)

**Error:** `Request rate is large. More Request Units may be needed`

**Solution:**
```json
{
  "CosmosDB": {
    "EnableAutoscale": true,
    "MaxThroughput": 10000  // Increase max RU/s
  }
}
```

#### Issue 3: Partition Key Mismatch

**Error:** `Partition key path does not match`

**Solution:** Ensure all queries include partition key:
```csharp
// ? Correct
await _context.Orders.WithPartitionKey(hotelCode).FirstOrDefaultAsync();

// ? Wrong
await _context.Orders.FirstOrDefaultAsync(); // Cross-partition query
```

#### Issue 4: TTL Not Working

**Symptom:** Documents not being deleted

**Check:**
1. Container has `DefaultTimeToLive = -1` (enables per-document TTL)
2. Documents have `ttl` property set
3. Wait for TTL background process (runs every 10 seconds in emulator)

```csharp
// Verify TTL is set
var order = await _orderRepository.GetOrderByIdAsync(id, hotelCode);
Console.WriteLine($"TTL: {order.Ttl}, Expires: {order.ExpiresAt}");
```

#### Issue 5: High Latency

**Solutions:**
1. Use `Direct` connection mode (not Gateway)
2. Co-locate app and Cosmos DB in same region
3. Enable bulk execution for batch operations
4. Use partition-scoped queries

```json
{
  "CosmosDB": {
    "ConnectionMode": "Direct",
    "ApplicationRegion": "East US",
    "AllowBulkExecution": true
  }
}
```

---

## Additional Resources

- [EF Core Cosmos Provider Documentation](https://learn.microsoft.com/en-us/ef/core/providers/cosmos/)
- [Azure Cosmos DB Documentation](https://learn.microsoft.com/en-us/azure/cosmos-db/)
- [Cosmos DB Emulator](https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator)
- [Best Practices](https://learn.microsoft.com/en-us/azure/cosmos-db/best-practice-dotnet)

---

## Support

For issues or questions:
1. Check [Troubleshooting](#troubleshooting) section
2. Review Cosmos DB logs in Application Insights
3. Enable detailed logging: `"EnableDetailedLogging": true`
4. Contact your team's infrastructure support

---

**Created:** 2024
**Framework Version:** .NET 8
**EF Core Version:** 8.0
**Azure Cosmos DB SDK Version:** 3.42+
