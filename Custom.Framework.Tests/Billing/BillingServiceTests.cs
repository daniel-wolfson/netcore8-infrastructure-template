using Custom.Framework.Billing.Models;
using Custom.Framework.Billing.Services;
using Moq;

namespace Custom.Framework.Tests.Billing;

/// <summary>
/// Tests for BillingService
/// </summary>
public class BillingServiceTests
{
    private readonly BillingService _service;

    public BillingServiceTests()
    {
        var _loggerMock = new Mock<ILogger<BillingService>>();
        _service = new BillingService(_loggerMock.Object);
    }

    [Fact]
    public async Task GetOrCreateBillingUser_ShouldCreateNewUser_WhenUserDoesNotExist()
    {
        // Arrange
        var userId = "user-123";

        // Act
        var billingUser = await _service.GetOrCreateBillingUserAsync(userId);

        // Assert
        Assert.NotNull(billingUser);
        Assert.Equal(userId, billingUser.UserId);
        Assert.Equal(0, billingUser.Balance);
        Assert.Equal("USD", billingUser.Currency);
    }

    [Fact]
    public async Task GetOrCreateBillingUser_ShouldReturnExistingUser_WhenUserExists()
    {
        // Arrange
        var userId = "user-123";
        var firstUser = await _service.GetOrCreateBillingUserAsync(userId);

        // Act
        var secondUser = await _service.GetOrCreateBillingUserAsync(userId);

        // Assert
        Assert.Equal(firstUser.Id, secondUser.Id);
        Assert.Equal(firstUser.UserId, secondUser.UserId);
    }

    [Fact]
    public async Task UpdateBalance_ShouldIncreaseBalance_WhenAmountIsPositive()
    {
        // Arrange
        var userId = "user-123";
        var billingUser = await _service.GetOrCreateBillingUserAsync(userId);
        var depositAmount = 100m;

        // Act
        var updatedUser = await _service.UpdateBalanceAsync(billingUser.Id, depositAmount);

        // Assert
        Assert.Equal(depositAmount, updatedUser.Balance);
    }

    [Fact]
    public async Task UpdateBalance_ShouldDecreaseBalance_WhenAmountIsNegative()
    {
        // Arrange
        var userId = "user-123";
        var billingUser = await _service.GetOrCreateBillingUserAsync(userId);
        await _service.UpdateBalanceAsync(billingUser.Id, 100m);

        // Act
        var updatedUser = await _service.UpdateBalanceAsync(billingUser.Id, -50m);

        // Assert
        Assert.Equal(50m, updatedUser.Balance);
    }

    [Fact]
    public async Task CreateTransaction_ShouldCreateNewTransaction()
    {
        // Arrange
        var userId = "user-123";
        var billingUser = await _service.GetOrCreateBillingUserAsync(userId);
        var transaction = new Transaction
        {
            BillingUserId = billingUser.Id.ToString(),
            Type = TransactionType.Deposit,
            Amount = 100m,
            Status = TransactionStatus.Pending,
            State = BillingTransactionState.Created
        };

        // Act
        var createdTransaction = await _service.CreateTransactionAsync(transaction);

        // Assert
        Assert.NotNull(createdTransaction);
        Assert.Equal(transaction.Amount, createdTransaction.Amount);
        Assert.Equal(TransactionType.Deposit, createdTransaction.Type);
    }

    [Fact]
    public async Task CreateSubscription_ShouldCreateNewSubscription()
    {
        // Arrange
        var userId = "user-123";
        var billingUser = await _service.GetOrCreateBillingUserAsync(userId);
        var subscription = new Subscription
        {
            BillingUserId = billingUser.Id.ToString(),
            PlanId = "premium",
            Amount = 99.99m,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        };

        // Act
        var createdSubscription = await _service.CreateSubscriptionAsync(subscription);

        // Assert
        Assert.NotNull(createdSubscription);
        Assert.Equal("premium", createdSubscription.PlanId);
        Assert.Equal(99.99m, createdSubscription.Amount);
        Assert.Equal(SubscriptionStatus.Active, createdSubscription.Status);
    }

    [Fact]
    public async Task GetActiveSubscription_ShouldReturnActiveSubscription()
    {
        // Arrange
        var userId = "user-123";
        var billingUser = await _service.GetOrCreateBillingUserAsync(userId);
        var subscription = new Subscription
        {
            BillingUserId = billingUser.Id.ToString(),
            PlanId = "premium",
            Amount = 99.99m,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        };
        await _service.CreateSubscriptionAsync(subscription);

        // Act
        var activeSubscription = await _service.GetActiveSubscriptionByUserIdAsync(userId);

        // Assert
        Assert.NotNull(activeSubscription);
        Assert.Equal("premium", activeSubscription.PlanId);
        Assert.Equal(SubscriptionStatus.Active, activeSubscription.Status);
    }

    [Fact]
    public async Task GetTransactionsByUserId_ShouldReturnUserTransactions()
    {
        // Arrange
        var userId = "user-123";
        var billingUser = await _service.GetOrCreateBillingUserAsync(userId);
        
        var transaction1 = new Transaction
        {
            BillingUserId = billingUser.Id.ToString(),
            Type = TransactionType.Deposit,
            Amount = 100m,
            Status = TransactionStatus.Completed,
            State = BillingTransactionState.Completed
        };

        var transaction2 = new Transaction
        {
            BillingUserId = billingUser.Id.ToString(),
            Type = TransactionType.Withdrawal,
            Amount = 50m,
            Status = TransactionStatus.Completed,
            State = BillingTransactionState.Completed
        };

        await _service.CreateTransactionAsync(transaction1);
        await _service.CreateTransactionAsync(transaction2);

        // Act
        var transactions = await _service.GetTransactionsByUserIdAsync(userId);

        // Assert
        Assert.Equal(2, transactions.Count());
    }
}
