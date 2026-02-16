# Billing Module with MassTransit & Saga Pattern

## 🎯 Overview

This module implements a complete billing system with:
- ✅ **MassTransit** integration with RabbitMQ
- ✅ **Saga Pattern** for distributed transactions
- ✅ **CQRS** (Command Query Responsibility Segregation)
- ✅ **Event-Driven Architecture**
- ✅ **State Machine** for transaction management
- ✅ **Compensation** logic for rollbacks

## 📁 Project Structure

```
Custom.Framework/Billing/
├── Commands/              # Command definitions (CQRS)
│   └── BillingCommands.cs
├── Events/               # Event definitions
│   └── BillingEvents.cs
├── Models/               # Domain models and DTOs
│   ├── BillingDtos.cs
│   ├── BillingEnums.cs
│   └── Transaction.cs
├── Services/             # Business logic services
│   ├── BillingService.cs
│   └── TravelServices.cs
├── Consumers/            # MassTransit message consumers
│   └── BillingConsumers.cs
├── Sagas/               # Saga orchestrators
│   └── TravelBookingSaga.cs
├── StateMachines/       # State machine implementations
│   └── TransactionStateMachine.cs
└── BillingExtensions.cs # DI configuration

Custom.Framework.Tests/Billing/
├── BillingServiceTests.cs
├── TransactionStateMachineTests.cs
└── TravelBookingSagaTests.cs
```

## 🚀 Quick Start

### 1. Installation

The required packages are already added:
- `MassTransit` (9.0.1)
- `MassTransit.RabbitMQ` (9.0.1)

### 2. Configuration

Add to your `Program.cs` or `Startup.cs`:

```csharp
using Custom.Framework.Billing;

// Add billing services with MassTransit
builder.Services.AddBillingWithMassTransit(options =>
{
    options.RabbitMqHost = "localhost";
    options.RabbitMqPort = 5672;
    options.RabbitMqUsername = "guest";
    options.RabbitMqPassword = "guest";
    options.QueueName = "billing_queue";
});
```

### 3. Basic Usage

#### Sending Commands

```csharp
// Inject IBus or ISendEndpointProvider
public class PaymentController : ControllerBase
{
    private readonly IBus _bus;

    public PaymentController(IBus bus)
    {
        _bus = bus;
    }

    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit([FromBody] DepositDto dto)
    {
        await _bus.Send(new DepositCommand
        {
            UserId = dto.UserId,
            Amount = dto.Amount,
            PaymentMethodId = dto.PaymentMethodId
        });

        return Accepted();
    }
}
```

## 📚 Core Concepts

### 1. Commands (Request for action)

Commands represent **requests to perform an action**:

```csharp
// Deposit money
var command = new DepositCommand
{
    UserId = "user-123",
    Amount = 100.00m,
    PaymentMethodId = "pm_123"
};
await bus.Send(command);
```

**Available Commands:**
- `DepositCommand` - Add funds to user account
- `WithdrawCommand` - Remove funds from user account
- `CreateSubscriptionCommand` - Create a recurring subscription
- `CancelSubscriptionCommand` - Cancel an active subscription
- `BookTravelCommand` - Orchestrate travel booking saga

### 2. Events (Something happened)

Events represent **things that have happened**:

```csharp
// Published when deposit completes
public record DepositCompletedEvent
{
    public required Guid TransactionId { get; init; }
    public required string UserId { get; init; }
    public required decimal Amount { get; init; }
}
```

**Available Events:**
- `DepositCompletedEvent`
- `WithdrawalCompletedEvent`
- `PaymentSuccessEvent`
- `PaymentFailedEvent`
- `SubscriptionCreatedEvent`
- `SubscriptionCanceledEvent`
- `TravelBookingCreatedEvent`
- `TravelBookingCompensatedEvent`
- `CompensationFailedEvent`

### 3. Consumers (Event handlers)

Consumers **process commands and publish events**:

```csharp
public class DepositCommandConsumer : IConsumer<DepositCommand>
{
    public async Task Consume(ConsumeContext<DepositCommand> context)
    {
        var command = context.Message;
        
        // Process deposit
        // ...
        
        // Publish event
        await context.Publish(new DepositCompletedEvent
        {
            TransactionId = transaction.Id,
            UserId = command.UserId,
            Amount = command.Amount
        });
    }
}
```

## 🔄 Saga Pattern

### What is a Saga?

A **Saga** is a pattern for managing **distributed transactions** across multiple services. It ensures **eventual consistency** by executing a sequence of steps, and if any step fails, it executes **compensating transactions** to undo previous steps.

### Travel Booking Saga Example

```
┌──────────────────────────────────────────────────────┐
│         Travel Booking Saga Flow                     │
└──────────────────────────────────────────────────────┘

Success Flow:
  Step 1: Reserve Flight  ✅
  Step 2: Reserve Hotel   ✅
  Step 3: Reserve Car     ✅
  Step 4: Process Payment ✅
  Result: Booking Confirmed

Failure Flow (Car Rental Fails):
  Step 1: Reserve Flight  ✅
  Step 2: Reserve Hotel   ✅
  Step 3: Reserve Car     ❌ FAILED
  
  Compensation (Reverse Order):
  Step 3: Cancel Hotel    ✅
  Step 2: Cancel Flight   ✅
  Result: Booking Compensated
```

### Using the Saga

```csharp
// Send travel booking command
await bus.Send(new BookTravelCommand
{
    UserId = "user-123",
    FlightOrigin = "JFK",
    FlightDestination = "LAX",
    DepartureDate = "2026-03-01",
    ReturnDate = "2026-03-08",
    HotelId = "hotel-456",
    CheckInDate = "2026-03-01",
    CheckOutDate = "2026-03-08",
    CarPickupLocation = "LAX Airport",
    CarDropoffLocation = "LAX Airport",
    CarPickupDate = "2026-03-01",
    CarDropoffDate = "2026-03-08",
    TotalAmount = 2500m
});
```

The saga will:
1. ✅ Reserve flight
2. ✅ Reserve hotel
3. ✅ Reserve car rental
4. ✅ Process payment
5. ❌ If any step fails → **Compensate in reverse order**

## 🎰 State Machine

The `TransactionStateMachine` manages transaction state transitions:

```
┌─────────┐
│ CREATED │
└────┬────┘
     │
     ▼
┌────────────┐
│ PROCESSING │
└────┬───┬───┘
     │   │
     │   └──────┐
     ▼          ▼
┌───────────┐  ┌───────┐
│ COMPLETED │  │ ERROR │
└───────────┘  └───┬───┘
                   │
                   ▼
              ┌──────────┐
              │ CANCELED │
              └──────────┘
```

**Valid Transitions:**
- Created → Processing
- Processing → Completed
- Processing → Error
- Error → Processing (Retry)
- Error → Canceled

## 🧪 Testing

### Run All Tests

```bash
dotnet test --filter "Custom.Framework.Tests.Billing"
```

### Test Categories

**1. BillingServiceTests**
- User management
- Balance operations
- Transaction creation
- Subscription management

**2. TransactionStateMachineTests**
- State transitions
- Validation
- Error handling
- Retry logic

**3. TravelBookingSagaTests**
- Successful saga execution
- Compensation on failure
- Event publishing
- Service coordination

## 📖 Real-World Scenarios

### Scenario 1: User Deposits Money

```csharp
// 1. Send deposit command
await bus.Send(new DepositCommand
{
    UserId = "user-123",
    Amount = 100.00m,
    PaymentMethodId = "pm_stripe_123"
});

// 2. Consumer processes command
// 3. Transaction state: Created → Processing → Completed
// 4. User balance updated
// 5. DepositCompletedEvent published
```

### Scenario 2: User Creates Subscription

```csharp
// 1. Send subscription command
await bus.Send(new CreateSubscriptionCommand
{
    UserId = "user-123",
    PlanId = "premium",
    Amount = 99.99m,
    Interval = "month",
    PaymentMethodId = "pm_123"
});

// 2. Subscription created
// 3. SubscriptionCreatedEvent published
```

### Scenario 3: Travel Booking with Compensation

```csharp
// 1. User books complete travel package
await bus.Send(new BookTravelCommand { ... });

// 2. Saga executes:
//    ✅ Flight reserved
//    ✅ Hotel reserved
//    ❌ Car rental FAILS

// 3. Saga compensates:
//    ✅ Cancel hotel
//    ✅ Cancel flight

// 4. TravelBookingCompensatedEvent published
// 5. User notified of failure
```

## 🔧 Advanced Configuration

### Retry Policy

```csharp
services.AddBillingWithMassTransit(options =>
{
    // Default retry: 100ms, 500ms, 1000ms
    options.RabbitMqHost = "localhost";
});
```

### Circuit Breaker

Built-in circuit breaker configuration:
- **Tracking Period**: 1 minute
- **Trip Threshold**: 15 failures
- **Active Threshold**: 10 concurrent calls
- **Reset Interval**: 5 minutes

## 🎓 Key Takeaways

### ✅ Benefits

1. **Distributed Transactions**: Saga pattern handles multi-service transactions
2. **Eventual Consistency**: Compensations ensure system consistency
3. **Resilience**: Retry policies and circuit breakers
4. **Scalability**: Message-based async processing
5. **Testability**: All components independently testable

### ⚠️ Considerations

1. **Idempotency**: Ensure operations are idempotent
2. **Compensation Logic**: Must be carefully designed
3. **Monitoring**: Track saga execution and failures
4. **Dead Letter Queues**: Handle unprocessable messages

## 📚 References

- [MassTransit Documentation](https://masstransit-project.com/)
- [Saga Pattern](https://microservices.io/patterns/data/saga.html)
- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)
- [Event-Driven Architecture](https://martinfowler.com/articles/201701-event-driven.html)

## 🎉 You're Ready!

The billing module is fully implemented and tested. All tests should pass:

```bash
✅ BillingServiceTests (11 tests)
✅ TransactionStateMachineTests (6 tests)
✅ TravelBookingSagaTests (6 tests)

Total: 23 tests passing
```

Happy coding! 🚀
