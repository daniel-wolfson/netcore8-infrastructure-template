using Custom.Framework.Billing.Models;
using Microsoft.Extensions.Logging;

namespace Custom.Framework.Billing.StateMachines;

/// <summary>
/// State machine for managing transaction state transitions
/// </summary>
public class TransactionStateMachine
{
    private readonly ILogger<TransactionStateMachine> _logger;
    private readonly Services.IBillingService _billingService;

    // Define valid state transitions
    private static readonly Dictionary<(BillingTransactionState From, BillingTransactionState To), bool> ValidTransitions = new()
    {
        { (BillingTransactionState.Created, BillingTransactionState.Processing), true },
        { (BillingTransactionState.Processing, BillingTransactionState.Completed), true },
        { (BillingTransactionState.Processing, BillingTransactionState.Error), true },
        { (BillingTransactionState.Created, BillingTransactionState.Canceled), true },
        { (BillingTransactionState.Processing, BillingTransactionState.Canceled), true },
        { (BillingTransactionState.Error, BillingTransactionState.Processing), true }, // Retry
        { (BillingTransactionState.Error, BillingTransactionState.Canceled), true }
    };

    public TransactionStateMachine(
        ILogger<TransactionStateMachine> logger,
        Services.IBillingService billingService)
    {
        _logger = logger;
        _billingService = billingService;
    }

    /// <summary>
    /// Attempt to transition a transaction to a new state
    /// </summary>
    public async Task<bool> TransitionAsync(Guid transactionId, BillingTransactionState newState, CancellationToken cancellationToken = default)
    {
        var transaction = await _billingService.GetTransactionByIdAsync(transactionId, cancellationToken);
        if (transaction == null)
        {
            throw new InvalidOperationException($"Transaction {transactionId} not found");
        }

        var currentState = transaction.State;

        // Check if transition is valid
        if (!IsValidTransition(currentState, newState))
        {
            _logger.LogError(
                "Invalid state transition for transaction {TransactionId}: {CurrentState} -> {NewState}",
                transactionId, currentState, newState);
            throw new InvalidOperationException($"Invalid state transition: {currentState} -> {newState}");
        }

        // Perform the state transition
        await _billingService.UpdateTransactionAsync(transactionId, tx =>
        {
            tx.State = newState;
            tx.Status = MapStateToStatus(newState);
            if (newState == BillingTransactionState.Completed)
            {
                tx.CompletedAt = DateTime.UtcNow;
            }
        }, cancellationToken);

        _logger.LogInformation(
            "Transaction {TransactionId} transitioned from {CurrentState} to {NewState}",
            transactionId, currentState, newState);

        // Execute side effects based on the new state
        await ExecuteStateActionsAsync(transactionId, newState, cancellationToken);

        return true;
    }

    /// <summary>
    /// Check if a state transition is valid
    /// </summary>
    private bool IsValidTransition(BillingTransactionState from, BillingTransactionState to)
    {
        // Allow staying in the same state
        if (from == to)
        {
            return true;
        }

        return ValidTransitions.TryGetValue((from, to), out var isValid) && isValid;
    }

    /// <summary>
    /// Map state to transaction status
    /// </summary>
    private TransactionStatus MapStateToStatus(BillingTransactionState state)
    {
        return state switch
        {
            BillingTransactionState.Created => TransactionStatus.Pending,
            BillingTransactionState.Processing => TransactionStatus.Processing,
            BillingTransactionState.Completed => TransactionStatus.Completed,
            BillingTransactionState.Error => TransactionStatus.Failed,
            BillingTransactionState.Canceled => TransactionStatus.Canceled,
            _ => TransactionStatus.Pending
        };
    }

    /// <summary>
    /// Execute side effects based on the new state
    /// </summary>
    private async Task ExecuteStateActionsAsync(Guid transactionId, BillingTransactionState newState, CancellationToken cancellationToken)
    {
        switch (newState)
        {
            case BillingTransactionState.Completed:
                _logger.LogInformation("Transaction {TransactionId} completed successfully", transactionId);
                // Here you could publish events, send notifications, etc.
                break;

            case BillingTransactionState.Error:
                _logger.LogWarning("Transaction {TransactionId} entered error state", transactionId);
                // Here you could trigger retry logic, alerts, etc.
                break;

            case BillingTransactionState.Canceled:
                _logger.LogInformation("Transaction {TransactionId} was canceled", transactionId);
                break;
        }

        await Task.CompletedTask;
    }
}
