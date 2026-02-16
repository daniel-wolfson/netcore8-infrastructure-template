using Custom.Framework.Billing.Models;
using Microsoft.Extensions.Logging;

namespace Custom.Framework.Billing.Services;

/// <summary>
/// In-memory implementation of billing service for demonstration
/// </summary>
public class BillingService : IBillingService
{
    private readonly ILogger<BillingService> _logger;
    private readonly Dictionary<Guid, BillingUser> _billingUsers = new();
    private readonly Dictionary<string, Guid> _userIdToBillingUserId = new();
    private readonly Dictionary<Guid, Transaction> _transactions = new();
    private readonly Dictionary<Guid, Subscription> _subscriptions = new();
    private readonly Dictionary<Guid, Invoice> _invoices = new();

    public BillingService(ILogger<BillingService> logger)
    {
        _logger = logger;
    }

    public Task<BillingUser> GetOrCreateBillingUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (_userIdToBillingUserId.TryGetValue(userId, out var billingUserId) &&
            _billingUsers.TryGetValue(billingUserId, out var existingUser))
        {
            return Task.FromResult(existingUser);
        }

        var newUser = new BillingUser
        {
            UserId = userId,
            Balance = 0,
            Currency = "USD"
        };

        _billingUsers[newUser.Id] = newUser;
        _userIdToBillingUserId[userId] = newUser.Id;

        _logger.LogInformation("Created billing user for userId: {UserId}", userId);
        return Task.FromResult(newUser);
    }

    public Task<BillingUser> UpdateBalanceAsync(Guid billingUserId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (!_billingUsers.TryGetValue(billingUserId, out var user))
        {
            throw new System.Collections.Generic.KeyNotFoundException($"Billing user {billingUserId} not found");
        }

        user.Balance += amount;
        user.UpdatedAt = DateTime.UtcNow;

        _logger.LogInformation("Updated balance for billing user {BillingUserId}: {Amount} (New Balance: {Balance})",
            billingUserId, amount, user.Balance);

        return Task.FromResult(user);
    }

    public Task<BillingUser?> GetBillingUserByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (_userIdToBillingUserId.TryGetValue(userId, out var billingUserId) &&
            _billingUsers.TryGetValue(billingUserId, out var user))
        {
            return Task.FromResult<BillingUser?>(user);
        }

        return Task.FromResult<BillingUser?>(null);
    }

    public Task<Transaction> CreateTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        _transactions[transaction.Id] = transaction;
        _logger.LogInformation("Created transaction {TransactionId} of type {Type} for {Amount}",
            transaction.Id, transaction.Type, transaction.Amount);
        return Task.FromResult(transaction);
    }

    public Task<Transaction?> UpdateTransactionAsync(Guid transactionId, Action<Transaction> updateAction, CancellationToken cancellationToken = default)
    {
        if (!_transactions.TryGetValue(transactionId, out var transaction))
        {
            return Task.FromResult<Transaction?>(null);
        }

        updateAction(transaction);
        _logger.LogInformation("Updated transaction {TransactionId}", transactionId);
        return Task.FromResult<Transaction?>(transaction);
    }

    public Task<Transaction?> GetTransactionByIdAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        _transactions.TryGetValue(transactionId, out var transaction);
        return Task.FromResult(transaction);
    }

    public Task<Subscription> CreateSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        _subscriptions[subscription.Id] = subscription;
        _logger.LogInformation("Created subscription {SubscriptionId} for plan {PlanId}",
            subscription.Id, subscription.PlanId);
        return Task.FromResult(subscription);
    }

    public Task<Subscription?> UpdateSubscriptionAsync(Guid subscriptionId, Action<Subscription> updateAction, CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out var subscription))
        {
            return Task.FromResult<Subscription?>(null);
        }

        updateAction(subscription);
        _logger.LogInformation("Updated subscription {SubscriptionId}", subscriptionId);
        return Task.FromResult<Subscription?>(subscription);
    }

    public Task<Subscription?> GetSubscriptionByIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        _subscriptions.TryGetValue(subscriptionId, out var subscription);
        return Task.FromResult(subscription);
    }

    public Task<Subscription?> GetActiveSubscriptionByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!_userIdToBillingUserId.TryGetValue(userId, out var billingUserId))
        {
            return Task.FromResult<Subscription?>(null);
        }

        var subscription = _subscriptions.Values
            .Where(s => s.BillingUserId == billingUserId.ToString() &&
                       (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();

        return Task.FromResult(subscription);
    }

    public Task<Invoice> CreateInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        _invoices[invoice.Id] = invoice;
        _logger.LogInformation("Created invoice {InvoiceId} for {Amount}",
            invoice.Id, invoice.Amount);
        return Task.FromResult(invoice);
    }

    public Task<IEnumerable<Transaction>> GetTransactionsByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!_userIdToBillingUserId.TryGetValue(userId, out var billingUserId))
        {
            return Task.FromResult(Enumerable.Empty<Transaction>());
        }

        var transactions = _transactions.Values
            .Where(t => t.BillingUserId == billingUserId.ToString())
            .OrderByDescending(t => t.CreatedAt);

        return Task.FromResult<IEnumerable<Transaction>>(transactions);
    }

    public Task<IEnumerable<Invoice>> GetInvoicesByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!_userIdToBillingUserId.TryGetValue(userId, out var billingUserId))
        {
            return Task.FromResult(Enumerable.Empty<Invoice>());
        }

        var invoices = _invoices.Values
            .Where(i => i.BillingUserId == billingUserId.ToString())
            .OrderByDescending(i => i.CreatedAt);

        return Task.FromResult<IEnumerable<Invoice>>(invoices);
    }
}
