using Custom.Framework.Billing.Models;

namespace Custom.Framework.Billing.Services;

/// <summary>
/// Service for managing billing users and transactions
/// </summary>
public interface IBillingService
{
    Task<BillingUser> GetOrCreateBillingUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<BillingUser> UpdateBalanceAsync(Guid billingUserId, decimal amount, CancellationToken cancellationToken = default);
    Task<BillingUser?> GetBillingUserByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<Transaction> CreateTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<Transaction?> UpdateTransactionAsync(Guid transactionId, Action<Transaction> updateAction, CancellationToken cancellationToken = default);
    Task<Transaction?> GetTransactionByIdAsync(Guid transactionId, CancellationToken cancellationToken = default);
    Task<Subscription> CreateSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken = default);
    Task<Subscription?> UpdateSubscriptionAsync(Guid subscriptionId, Action<Subscription> updateAction, CancellationToken cancellationToken = default);
    Task<Subscription?> GetSubscriptionByIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
    Task<Subscription?> GetActiveSubscriptionByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<Invoice> CreateInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task<IEnumerable<Transaction>> GetTransactionsByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Invoice>> GetInvoicesByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
