using Custom.Framework.Billing.Commands;
using Custom.Framework.Billing.Events;
using Custom.Framework.Billing.Models;
using Custom.Framework.Billing.Services;
using Custom.Framework.Billing.StateMachines;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Custom.Framework.Billing.Consumers;

/// <summary>
/// Consumer for Deposit commands
/// </summary>
public class DepositCommandConsumer : IConsumer<DepositCommand>
{
    private readonly ILogger<DepositCommandConsumer> _logger;
    private readonly IBillingService _billingService;
    private readonly TransactionStateMachine _stateMachine;

    public DepositCommandConsumer(
        ILogger<DepositCommandConsumer> logger,
        IBillingService billingService,
        TransactionStateMachine stateMachine)
    {
        _logger = logger;
        _billingService = billingService;
        _stateMachine = stateMachine;
    }

    public async Task Consume(ConsumeContext<DepositCommand> context)
    {
        var command = context.Message;
        _logger.LogInformation("Processing deposit for user {UserId}, amount: {Amount}", command.UserId, command.Amount);

        try
        {
            // Get or create billing user
            var billingUser = await _billingService.GetOrCreateBillingUserAsync(command.UserId, context.CancellationToken);

            // Create transaction in CREATED state
            var transaction = await _billingService.CreateTransactionAsync(new Transaction
            {
                BillingUserId = billingUser.Id.ToString(),
                Type = TransactionType.Deposit,
                Amount = command.Amount,
                Status = TransactionStatus.Pending,
                State = BillingTransactionState.Created,
                PaymentMethodId = command.PaymentMethodId,
                Description = $"Deposit of {command.Amount}",
                IdempotencyKey = $"deposit-{command.UserId}-{DateTime.UtcNow.Ticks}"
            }, context.CancellationToken);

            // Transition to PROCESSING
            await _stateMachine.TransitionAsync(transaction.Id, BillingTransactionState.Processing, context.CancellationToken);

            // Simulate payment processing (in real scenario, integrate with Stripe)
            await Task.Delay(100, context.CancellationToken);

            // Update balance and complete transaction
            await _billingService.UpdateBalanceAsync(billingUser.Id, command.Amount, context.CancellationToken);
            await _stateMachine.TransitionAsync(transaction.Id, BillingTransactionState.Completed, context.CancellationToken);

            // Publish event
            await context.Publish(new DepositCompletedEvent
            {
                TransactionId = transaction.Id,
                UserId = command.UserId,
                Amount = command.Amount
            });

            _logger.LogInformation("Deposit completed successfully for user {UserId}", command.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process deposit for user {UserId}", command.UserId);
            throw;
        }
    }
}

/// <summary>
/// Consumer for Withdraw commands
/// </summary>
public class WithdrawCommandConsumer : IConsumer<WithdrawCommand>
{
    private readonly ILogger<WithdrawCommandConsumer> _logger;
    private readonly IBillingService _billingService;
    private readonly TransactionStateMachine _stateMachine;

    public WithdrawCommandConsumer(
        ILogger<WithdrawCommandConsumer> logger,
        IBillingService billingService,
        TransactionStateMachine stateMachine)
    {
        _logger = logger;
        _billingService = billingService;
        _stateMachine = stateMachine;
    }

    public async Task Consume(ConsumeContext<WithdrawCommand> context)
    {
        var command = context.Message;
        _logger.LogInformation("Processing withdrawal for user {UserId}, amount: {Amount}", command.UserId, command.Amount);

        try
        {
            // Get billing user
            var billingUser = await _billingService.GetBillingUserByUserIdAsync(command.UserId, context.CancellationToken);
            if (billingUser == null)
            {
                throw new InvalidOperationException($"Billing user not found for userId: {command.UserId}");
            }

            // Check balance
            if (billingUser.Balance < command.Amount)
            {
                throw new InvalidOperationException("Insufficient balance");
            }

            // Create transaction
            var transaction = await _billingService.CreateTransactionAsync(new Transaction
            {
                BillingUserId = billingUser.Id.ToString(),
                Type = TransactionType.Withdrawal,
                Amount = command.Amount,
                Status = TransactionStatus.Pending,
                State = BillingTransactionState.Created,
                Description = $"Withdrawal of {command.Amount}",
                IdempotencyKey = $"withdraw-{command.UserId}-{DateTime.UtcNow.Ticks}"
            }, context.CancellationToken);

            // Transition to PROCESSING
            await _stateMachine.TransitionAsync(transaction.Id, BillingTransactionState.Processing, context.CancellationToken);

            // Update balance and complete transaction
            await _billingService.UpdateBalanceAsync(billingUser.Id, -command.Amount, context.CancellationToken);
            await _stateMachine.TransitionAsync(transaction.Id, BillingTransactionState.Completed, context.CancellationToken);

            // Publish event
            await context.Publish(new WithdrawalCompletedEvent
            {
                TransactionId = transaction.Id,
                UserId = command.UserId,
                Amount = command.Amount
            });

            _logger.LogInformation("Withdrawal completed successfully for user {UserId}", command.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process withdrawal for user {UserId}", command.UserId);
            throw;
        }
    }
}

/// <summary>
/// Consumer for CreateSubscription commands
/// </summary>
public class CreateSubscriptionCommandConsumer : IConsumer<CreateSubscriptionCommand>
{
    private readonly ILogger<CreateSubscriptionCommandConsumer> _logger;
    private readonly IBillingService _billingService;

    public CreateSubscriptionCommandConsumer(
        ILogger<CreateSubscriptionCommandConsumer> logger,
        IBillingService billingService)
    {
        _logger = logger;
        _billingService = billingService;
    }

    public async Task Consume(ConsumeContext<CreateSubscriptionCommand> context)
    {
        var command = context.Message;
        _logger.LogInformation("Creating subscription for user {UserId}, plan: {PlanId}", command.UserId, command.PlanId);

        try
        {
            var billingUser = await _billingService.GetOrCreateBillingUserAsync(command.UserId, context.CancellationToken);

            var subscription = await _billingService.CreateSubscriptionAsync(new Subscription
            {
                BillingUserId = billingUser.Id.ToString(),
                PlanId = command.PlanId,
                Status = SubscriptionStatus.Active,
                Amount = command.Amount,
                Currency = "USD",
                Interval = command.Interval,
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
            }, context.CancellationToken);

            await context.Publish(new SubscriptionCreatedEvent
            {
                SubscriptionId = subscription.Id,
                UserId = command.UserId,
                PlanId = command.PlanId,
                Amount = command.Amount
            });

            _logger.LogInformation("Subscription created successfully: {SubscriptionId}", subscription.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create subscription for user {UserId}", command.UserId);
            throw;
        }
    }
}

/// <summary>
/// Consumer for CancelSubscription commands
/// </summary>
public class CancelSubscriptionCommandConsumer : IConsumer<CancelSubscriptionCommand>
{
    private readonly ILogger<CancelSubscriptionCommandConsumer> _logger;
    private readonly IBillingService _billingService;

    public CancelSubscriptionCommandConsumer(
        ILogger<CancelSubscriptionCommandConsumer> logger,
        IBillingService billingService)
    {
        _logger = logger;
        _billingService = billingService;
    }

    public async Task Consume(ConsumeContext<CancelSubscriptionCommand> context)
    {
        var command = context.Message;
        _logger.LogInformation("Canceling subscription: {SubscriptionId}", command.SubscriptionId);

        try
        {
            var subscriptionId = Guid.Parse(command.SubscriptionId);
            var subscription = await _billingService.GetSubscriptionByIdAsync(subscriptionId, context.CancellationToken);

            if (subscription == null)
            {
                throw new InvalidOperationException($"Subscription not found: {command.SubscriptionId}");
            }

            await _billingService.UpdateSubscriptionAsync(subscriptionId, sub =>
            {
                sub.Status = SubscriptionStatus.Canceled;
                sub.CanceledAt = DateTime.UtcNow;
                sub.EndedAt = DateTime.UtcNow;
            }, context.CancellationToken);

            await context.Publish(new SubscriptionCanceledEvent
            {
                SubscriptionId = subscriptionId,
                UserId = subscription.BillingUserId,
                Reason = command.Reason
            });

            _logger.LogInformation("Subscription canceled successfully: {SubscriptionId}", command.SubscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel subscription: {SubscriptionId}", command.SubscriptionId);
            throw;
        }
    }
}

/// <summary>
/// Consumer for BookTravel commands (Saga orchestration)
/// </summary>
public class BookTravelCommandConsumer : IConsumer<BookTravelCommand>
{
    private readonly ILogger<BookTravelCommandConsumer> _logger;
    private readonly Sagas.TravelBookingSaga _saga;

    public BookTravelCommandConsumer(
        ILogger<BookTravelCommandConsumer> logger,
        Sagas.TravelBookingSaga saga)
    {
        _logger = logger;
        _saga = saga;
    }

    public async Task Consume(ConsumeContext<BookTravelCommand> context)
    {
        var command = context.Message;
        _logger.LogInformation("Processing travel booking for user {UserId}", command.UserId);

        try
        {
            var dto = new TravelBookingDto
            {
                UserId = command.UserId,
                FlightOrigin = command.FlightOrigin,
                FlightDestination = command.FlightDestination,
                DepartureDate = command.DepartureDate,
                ReturnDate = command.ReturnDate,
                HotelId = command.HotelId,
                CheckInDate = command.CheckInDate,
                CheckOutDate = command.CheckOutDate,
                CarPickupLocation = command.CarPickupLocation,
                CarDropoffLocation = command.CarDropoffLocation,
                CarPickupDate = command.CarPickupDate,
                CarDropoffDate = command.CarDropoffDate,
                TotalAmount = command.TotalAmount
            };

            var result = await _saga.ExecuteAsync(dto, context.CancellationToken);

            _logger.LogInformation("Travel booking completed: {BookingId}, Status: {Status}",
                result.BookingId, result.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process travel booking for user {UserId}", command.UserId);
            throw;
        }
    }
}
