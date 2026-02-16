using Custom.Framework.Billing.Models;
using Custom.Framework.Billing.Services;
using Custom.Framework.Billing.StateMachines;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Custom.Framework.Tests.Billing;

/// <summary>
/// Tests for TransactionStateMachine
/// </summary>
public class TransactionStateMachineTests
{
    private readonly TransactionStateMachine _stateMachine;
    private readonly BillingService _billingService;
    private readonly Mock<ILogger<TransactionStateMachine>> _stateMachineLoggerMock;
    private readonly Mock<ILogger<BillingService>> _billingServiceLoggerMock;

    public TransactionStateMachineTests()
    {
        _stateMachineLoggerMock = new Mock<ILogger<TransactionStateMachine>>();
        _billingServiceLoggerMock = new Mock<ILogger<BillingService>>();
        _billingService = new BillingService(_billingServiceLoggerMock.Object);
        _stateMachine = new TransactionStateMachine(_stateMachineLoggerMock.Object, _billingService);
    }

    [Fact]
    public async Task Transition_ShouldSucceed_WhenTransitionIsValid()
    {
        // Arrange
        var transaction = new Transaction
        {
            BillingUserId = Guid.NewGuid().ToString(),
            Type = TransactionType.Deposit,
            Amount = 100m,
            Status = TransactionStatus.Pending,
            State = BillingTransactionState.Created
        };
        var createdTransaction = await _billingService.CreateTransactionAsync(transaction);

        // Act
        var result = await _stateMachine.TransitionAsync(createdTransaction.Id, BillingTransactionState.Processing);

        // Assert
        Assert.True(result);
        var updatedTransaction = await _billingService.GetTransactionByIdAsync(createdTransaction.Id);
        Assert.Equal(BillingTransactionState.Processing, updatedTransaction!.State);
        Assert.Equal(TransactionStatus.Processing, updatedTransaction.Status);
    }

    [Fact]
    public async Task Transition_ShouldThrowException_WhenTransitionIsInvalid()
    {
        // Arrange
        var transaction = new Transaction
        {
            BillingUserId = Guid.NewGuid().ToString(),
            Type = TransactionType.Deposit,
            Amount = 100m,
            Status = TransactionStatus.Pending,
            State = BillingTransactionState.Created
        };
        var createdTransaction = await _billingService.CreateTransactionAsync(transaction);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _stateMachine.TransitionAsync(createdTransaction.Id, BillingTransactionState.Completed));
    }

    [Fact]
    public async Task Transition_ShouldAllowRetry_FromErrorState()
    {
        // Arrange
        var transaction = new Transaction
        {
            BillingUserId = Guid.NewGuid().ToString(),
            Type = TransactionType.Deposit,
            Amount = 100m,
            Status = TransactionStatus.Failed,
            State = BillingTransactionState.Error
        };
        var createdTransaction = await _billingService.CreateTransactionAsync(transaction);

        // Act
        var result = await _stateMachine.TransitionAsync(createdTransaction.Id, BillingTransactionState.Processing);

        // Assert
        Assert.True(result);
        var updatedTransaction = await _billingService.GetTransactionByIdAsync(createdTransaction.Id);
        Assert.Equal(BillingTransactionState.Processing, updatedTransaction!.State);
    }

    [Fact]
    public async Task Transition_ShouldSetCompletedAt_WhenCompletedStateReached()
    {
        // Arrange
        var transaction = new Transaction
        {
            BillingUserId = Guid.NewGuid().ToString(),
            Type = TransactionType.Deposit,
            Amount = 100m,
            Status = TransactionStatus.Processing,
            State = BillingTransactionState.Processing
        };
        var createdTransaction = await _billingService.CreateTransactionAsync(transaction);

        // Act
        await _stateMachine.TransitionAsync(createdTransaction.Id, BillingTransactionState.Completed);

        // Assert
        var updatedTransaction = await _billingService.GetTransactionByIdAsync(createdTransaction.Id);
        Assert.NotNull(updatedTransaction!.CompletedAt);
        Assert.Equal(TransactionStatus.Completed, updatedTransaction.Status);
    }

    [Theory]
    [InlineData(BillingTransactionState.Created, BillingTransactionState.Processing, true)]
    [InlineData(BillingTransactionState.Processing, BillingTransactionState.Completed, true)]
    [InlineData(BillingTransactionState.Processing, BillingTransactionState.Error, true)]
    [InlineData(BillingTransactionState.Created, BillingTransactionState.Completed, false)]
    [InlineData(BillingTransactionState.Completed, BillingTransactionState.Processing, false)]
    public async Task Transition_ShouldValidateStateTransitions(
        BillingTransactionState fromState,
        BillingTransactionState toState,
        bool shouldSucceed)
    {
        // Arrange
        var transaction = new Transaction
        {
            BillingUserId = Guid.NewGuid().ToString(),
            Type = TransactionType.Deposit,
            Amount = 100m,
            Status = TransactionStatus.Pending,
            State = fromState
        };
        var createdTransaction = await _billingService.CreateTransactionAsync(transaction);

        // Act & Assert
        if (shouldSucceed)
        {
            var result = await _stateMachine.TransitionAsync(createdTransaction.Id, toState);
            Assert.True(result);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _stateMachine.TransitionAsync(createdTransaction.Id, toState));
        }
    }
}
