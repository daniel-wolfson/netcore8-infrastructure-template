# Billing Module Quick Start Guide

## ⚡ 5-Minute Setup

### Step 1: Start RabbitMQ

```bash
# Using Docker
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management

# Access Management UI
# http://localhost:15672 (guest/guest)
```

### Step 2: Configure Your Application

```csharp
// Program.cs
using Custom.Framework.Billing;

var builder = WebApplication.CreateBuilder(args);

// Add Billing with MassTransit
builder.Services.AddBillingWithMassTransit(options =>
{
    options.RabbitMqHost = "localhost";
    options.RabbitMqPort = 5672;
    options.RabbitMqUsername = "guest";
    options.RabbitMqPassword = "guest";
    options.QueueName = "billing_queue";
});

var app = builder.Build();
app.Run();
```

### Step 3: Send Your First Command

```csharp
using Custom.Framework.Billing.Commands;
using MassTransit;

[ApiController]
[Route("api/[controller]")]
public class BillingController : ControllerBase
{
    private readonly IBus _bus;

    public BillingController(IBus bus)
    {
        _bus = bus;
    }

    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit([FromBody] DepositRequest request)
    {
        await _bus.Send(new DepositCommand
        {
            UserId = request.UserId,
            Amount = request.Amount,
            PaymentMethodId = request.PaymentMethodId
        });

        return Accepted();
    }

    [HttpPost("travel-booking")]
    public async Task<IActionResult> BookTravel([FromBody] TravelBookingRequest request)
    {
        await _bus.Send(new BookTravelCommand
        {
            UserId = request.UserId,
            FlightOrigin = request.FlightOrigin,
            FlightDestination = request.FlightDestination,
            DepartureDate = request.DepartureDate,
            ReturnDate = request.ReturnDate,
            HotelId = request.HotelId,
            CheckInDate = request.CheckInDate,
            CheckOutDate = request.CheckOutDate,
            CarPickupLocation = request.CarPickupLocation,
            CarDropoffLocation = request.CarDropoffLocation,
            CarPickupDate = request.CarPickupDate,
            CarDropoffDate = request.CarDropoffDate,
            TotalAmount = request.TotalAmount
        });

        return Accepted();
    }
}
```

## 🧪 Run Tests

```bash
# Run all billing tests
dotnet test --filter "Custom.Framework.Tests.Billing"

# Run specific test class
dotnet test --filter "TravelBookingSagaTests"
```

## 📊 Common Scenarios

### 1. Deposit Money

```bash
POST /api/billing/deposit
{
  "userId": "user-123",
  "amount": 100.00,
  "paymentMethodId": "pm_card_visa"
}
```

### 2. Create Subscription

```bash
POST /api/billing/subscription
{
  "userId": "user-123",
  "planId": "premium",
  "amount": 99.99,
  "interval": "month"
}
```

### 3. Book Travel (Saga Pattern)

```bash
POST /api/billing/travel-booking
{
  "userId": "user-123",
  "flightOrigin": "JFK",
  "flightDestination": "LAX",
  "departureDate": "2026-03-01",
  "returnDate": "2026-03-08",
  "hotelId": "hotel-456",
  "checkInDate": "2026-03-01",
  "checkOutDate": "2026-03-08",
  "carPickupLocation": "LAX Airport",
  "carDropoffLocation": "LAX Airport",
  "carPickupDate": "2026-03-01",
  "carDropoffDate": "2026-03-08",
  "totalAmount": 2500.00
}
```

## 📝 Testing with cURL

```bash
# Deposit
curl -X POST http://localhost:5000/api/billing/deposit \
  -H "Content-Type: application/json" \
  -d '{"userId":"user-123","amount":100.00}'

# Travel Booking
curl -X POST http://localhost:5000/api/billing/travel-booking \
  -H "Content-Type: application/json" \
  -d '{
    "userId":"user-123",
    "flightOrigin":"JFK",
    "flightDestination":"LAX",
    "departureDate":"2026-03-01",
    "returnDate":"2026-03-08",
    "hotelId":"hotel-456",
    "checkInDate":"2026-03-01",
    "checkOutDate":"2026-03-08",
    "carPickupLocation":"LAX Airport",
    "carDropoffLocation":"LAX Airport",
    "carPickupDate":"2026-03-01",
    "carDropoffDate":"2026-03-08",
    "totalAmount":2500.00
  }'
```

## 🔍 Monitor RabbitMQ

1. Open: http://localhost:15672
2. Login: `guest` / `guest`
3. Check:
   - **Queues**: `billing_queue`
   - **Exchanges**: Message routing
   - **Messages**: Incoming/outgoing

## ✅ Verify Installation

```bash
# Build the solution
dotnet build

# Run tests
dotnet test

# Expected output:
# ✅ BillingServiceTests - 11 passed
# ✅ TransactionStateMachineTests - 6 passed
# ✅ TravelBookingSagaTests - 6 passed
```

## 🎯 Next Steps

1. Read the full [README.md](./README.md)
2. Explore the [Saga Pattern documentation](./README.md#-saga-pattern)
3. Check test files for more examples
4. Integrate with your existing services

## 🆘 Troubleshooting

### RabbitMQ Connection Failed

```bash
# Check if RabbitMQ is running
docker ps | grep rabbitmq

# Restart RabbitMQ
docker restart rabbitmq
```

### Tests Failing

```bash
# Clean and rebuild
dotnet clean
dotnet build
dotnet test
```

### Messages Not Being Consumed

1. Check RabbitMQ Management UI
2. Verify queue is declared
3. Check consumer is registered
4. Review logs for exceptions

## 📞 Support

For issues or questions:
1. Check the main [README.md](./README.md)
2. Review test files for examples
3. Check MassTransit documentation

---

**You're all set!** 🎉
