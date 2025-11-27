# Quick Start: Azure Cosmos DB for Hospitality Orders

Get up and running with Azure Cosmos DB in **5 minutes**!

---

## ?? Quick Setup (3 Steps)

### Step 1: Install NuGet Packages

```bash
cd Custom.Framework
dotnet add package Microsoft.EntityFrameworkCore.Cosmos
dotnet add package Microsoft.Azure.Cosmos
```

### Step 2: Add Configuration

**appsettings.Development.json:**
```json
{
  "CosmosDB": {
    "UseEmulator": true
  }
}
```

### Step 3: Register Services

**Program.cs:**
```csharp
using Custom.Framework.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// Add Cosmos DB
builder.Services.AddCosmosDbForOrders(builder.Configuration);

var app = builder.Build();

// Initialize database
app.UseCosmosDb();

app.Run();
```

**Done!** ??

---

## ?? Usage Example

### Create Order in Controller

```csharp
using Custom.Framework.Azure.Cosmos;
using Custom.Framework.Azure.Cosmos.Models;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;

    public OrderController(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartOrder([FromBody] StartOrderRequest request)
    {
        var order = new OrderContext
        {
            HotelCode = request.HotelCode,
            SessionId = HttpContext.Session.Id,
            Status = OrderStatus.Pending,
            CurrentStep = "SearchHeader",
            OrderData = new OrderData
            {
                CheckInDate = request.CheckInDate,
                CheckOutDate = request.CheckOutDate,
                Adults = request.Adults,
                Children = request.Children
            }
        };

        var created = await _orderRepository.CreateOrderAsync(order);
        
        return Ok(new { orderId = created.Id, expiresAt = created.ExpiresAt });
    }

    [HttpPut("{orderId}/update")]
    public async Task<IActionResult> UpdateOrder(string orderId, [FromBody] UpdateOrderRequest request)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId, request.HotelCode);
        if (order == null)
            return NotFound();

        order.CurrentStep = request.Step;
        order.OrderData.RoomCode = request.RoomCode;
        order.OrderData.PlanCode = request.PlanCode;
        order.OrderData.TotalAmount = request.TotalAmount;

        await _orderRepository.UpdateOrderAsync(order);

        return Ok(order);
    }

    [HttpPost("{orderId}/complete")]
    public async Task<IActionResult> CompleteOrder(string orderId, [FromBody] CompleteOrderRequest request)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId, request.HotelCode);
        if (order == null)
            return NotFound();

        order.Status = OrderStatus.Succeeded;
        order.PaymentInfo = new PaymentInfo
        {
            PaymentMethod = request.PaymentMethod,
            TransactionId = request.TransactionId,
            Amount = request.Amount,
            PaymentStatus = "Success"
        };

        await _orderRepository.UpdateOrderAsync(order);

        return Ok(new { message = "Order completed", order });
    }

    [HttpGet("session")]
    public async Task<IActionResult> GetCurrentOrder([FromQuery] string hotelCode)
    {
        var sessionId = HttpContext.Session.Id;
        var order = await _orderRepository.GetOrderBySessionIdAsync(sessionId, hotelCode);
        
        if (order == null)
            return NotFound();

        return Ok(order);
    }
}

public record StartOrderRequest(string HotelCode, DateTime CheckInDate, DateTime CheckOutDate, int Adults, int Children);
public record UpdateOrderRequest(string HotelCode, string Step, string? RoomCode, string? PlanCode, decimal? TotalAmount);
public record CompleteOrderRequest(string HotelCode, string PaymentMethod, string TransactionId, decimal Amount);
```

---

## ?? Local Development Setup

### 1. Start Cosmos DB Emulator

**Windows:**
```powershell
# Download and install emulator from:
# https://aka.ms/cosmosdb-emulator

# Or use chocolatey:
choco install azure-cosmosdb-emulator
```

**Linux/macOS (Docker):**
```bash
docker run -p 8081:8081 \
  --name cosmos-emulator \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
```

### 2. Trust Certificate (First Time Only)

**Windows:**
```powershell
curl -k https://localhost:8081/_explorer/emulator.pem > emulator.pem
Import-Certificate -FilePath .\emulator.pem -CertStoreLocation Cert:\CurrentUser\Root
```

### 3. Verify Emulator

Open browser: https://localhost:8081/_explorer/index.html

---

## ?? Test It

### Create Test Order

```bash
curl -X POST https://localhost:5001/api/order/start \
  -H "Content-Type: application/json" \
  -d '{
    "hotelCode": "HOTEL001",
    "checkInDate": "2024-12-20",
    "checkOutDate": "2024-12-22",
    "adults": 2,
    "children": 0
  }'
```

**Response:**
```json
{
  "orderId": "abc123...",
  "expiresAt": "2024-12-15T10:20:00Z"
}
```

### Update Order

```bash
curl -X PUT https://localhost:5001/api/order/{orderId}/update \
  -H "Content-Type: application/json" \
  -d '{
    "hotelCode": "HOTEL001",
    "step": "SelectHeader",
    "roomCode": "DELUXE",
    "planCode": "BB",
    "totalAmount": 299.99
  }'
```

### Complete Order

```bash
curl -X POST https://localhost:5001/api/order/{orderId}/complete \
  -H "Content-Type: application/json" \
  -d '{
    "hotelCode": "HOTEL001",
    "paymentMethod": "CreditCard",
    "transactionId": "TXN123",
    "amount": 299.99
  }'
```

---

## ?? View Data in Emulator

1. Open: https://localhost:8081/_explorer/index.html
2. Navigate to: **HospitalityOrders** ? **Orders**
3. See your documents!

---

## ?? TTL Behavior

| Status | TTL | Auto-Delete |
|--------|-----|-------------|
| Pending | 10 min | ? Yes |
| PaymentInProgress | 10 min | ? Yes |
| Succeeded | 7 days | ? Yes |
| Failed | 10 min | ? Yes |

**Test TTL:**
```csharp
// Create order
var order = await _orderRepository.CreateOrderAsync(new OrderContext
{
    HotelCode = "TEST",
    Status = OrderStatus.Pending,
    // TTL = 600 seconds (10 min) - auto-set by repository
});

Console.WriteLine($"Order expires at: {order.ExpiresAt}");
// Order expires at: 2024-12-15T10:20:00Z

// Wait 10 minutes... order is automatically deleted by Cosmos DB!
```

---

## ?? Common Patterns

### Pattern 1: Session-Based Order Tracking

```csharp
// Start reservation
var order = new OrderContext
{
    HotelCode = "HOTEL001",
    SessionId = HttpContext.Session.Id,
    Status = OrderStatus.Pending
};
await _orderRepository.CreateOrderAsync(order);

// Later, retrieve by session
var currentOrder = await _orderRepository.GetOrderBySessionIdAsync(
    HttpContext.Session.Id, 
    "HOTEL001"
);
```

### Pattern 2: Multi-Step Order Flow

```csharp
public class OrderFlowService
{
    private readonly IOrderRepository _repo;

    public async Task<string> StartFlow(string hotelCode, string sessionId)
    {
        var order = new OrderContext
        {
            HotelCode = hotelCode,
            SessionId = sessionId,
            CurrentStep = "SearchHeader"
        };
        
        var created = await _repo.CreateOrderAsync(order);
        return created.Id;
    }

    public async Task MoveToNextStep(string orderId, string hotelCode, string nextStep)
    {
        await _repo.UpdateOrderStepAsync(orderId, hotelCode, nextStep);
    }

    public async Task CompleteFlow(string orderId, string hotelCode)
    {
        await _repo.UpdateOrderStatusAsync(orderId, hotelCode, OrderStatus.Succeeded);
    }
}
```

### Pattern 3: Error Handling

```csharp
try
{
    var order = await _orderRepository.GetOrderByIdAsync(orderId, hotelCode);
    if (order == null)
    {
        return NotFound("Order not found or expired");
    }

    // Update order...
    await _orderRepository.UpdateOrderAsync(order);
}
catch (DbUpdateConcurrencyException)
{
    // Another request modified the order
    return Conflict("Order was modified by another request");
}
catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
{
    // Throttled - retry
    return StatusCode(429, "Service temporarily unavailable");
}
```

---

## ?? Next Steps

1. ? Read full documentation: [AzureCosmos-Integration.md](./AzureCosmos-Integration.md)
2. ? Configure production settings
3. ? Add monitoring and logging
4. ? Implement cleanup jobs for expired orders
5. ? Add unit tests

---

## ?? Full Documentation

For complete documentation, see: **[AzureCosmos-Integration.md](./AzureCosmos-Integration.md)**

Topics covered:
- Advanced configuration
- Performance optimization
- Production deployment
- Monitoring and troubleshooting
- Best practices

---

**Questions?** Check the [Troubleshooting](./AzureCosmos-Integration.md#troubleshooting) section in the full docs!
