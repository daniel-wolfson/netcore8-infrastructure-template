using Custom.Framework.Billing.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Custom.Framework.Billing.Consumers;

/// <summary>
/// Consumer for Dead Letter Queue events
/// Handles failed compensation transactions that require manual intervention
/// </summary>
public class DeadLetterQueueConsumer : IConsumer<DeadLetterQueueEvent>
{
    private readonly ILogger<DeadLetterQueueConsumer> _logger;

    public DeadLetterQueueConsumer(ILogger<DeadLetterQueueConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DeadLetterQueueEvent> context)
    {
        var dlq = context.Message;

        _logger.LogCritical(
            "💀 DLQ Event Received | CorrelationId: {CorrelationId} | BookingId: {BookingId} | Severity: {Severity}",
            dlq.CorrelationId,
            dlq.BookingId,
            dlq.Severity);

        _logger.LogCritical(
            "Failed Service: {Service} | Action: {Action} | Error: {Error}",
            dlq.FailedService,
            dlq.CompensationAction,
            dlq.ErrorMessage);

        // Log reservation details for manual processing
        if (!string.IsNullOrEmpty(dlq.FlightReservationId))
        {
            _logger.LogWarning("Flight Reservation: {ReservationId} - Requires manual cancellation", dlq.FlightReservationId);
        }

        if (!string.IsNullOrEmpty(dlq.HotelReservationId))
        {
            _logger.LogWarning("Hotel Reservation: {ReservationId} - Requires manual cancellation", dlq.HotelReservationId);
        }

        if (!string.IsNullOrEmpty(dlq.CarRentalReservationId))
        {
            _logger.LogWarning("Car Rental Reservation: {ReservationId} - Requires manual cancellation", dlq.CarRentalReservationId);
        }

        // In a real implementation, you would:
        // 1. Store the DLQ event in a database
        // 2. Send alerts based on severity (PagerDuty, email, Slack)
        // 3. Create a ticket in your ticketing system
        // 4. Update a dashboard for monitoring
        // 5. Trigger automated retry workflows if applicable

        // Example: Send alert based on severity
        switch (dlq.Severity)
        {
            case "Critical":
                _logger.LogCritical("🚨 CRITICAL SEVERITY - Immediate attention required!");
                // await _pagerDutyService.SendAlert(dlq);
                // await _slackService.SendMessage("#critical-alerts", dlq);
                break;

            case "High":
                _logger.LogError("⚠️ HIGH SEVERITY - Requires prompt attention");
                // await _emailService.SendAlert(dlq);
                // await _slackService.SendMessage("#alerts", dlq);
                break;

            case "Medium":
                _logger.LogWarning("ℹ️ MEDIUM SEVERITY - Review during business hours");
                // await _emailService.SendNotification(dlq);
                break;
        }

        // Log additional context
        if (dlq.AdditionalContext != null && dlq.AdditionalContext.Any())
        {
            _logger.LogInformation("Additional Context:");
            foreach (var kvp in dlq.AdditionalContext)
            {
                _logger.LogInformation("  {Key}: {Value}", kvp.Key, kvp.Value);
            }
        }

        _logger.LogInformation(
            "DLQ Event Processing Complete | Duration: {Duration}s | Retry Count: {RetryCount}",
            (dlq.FailedAttemptTime - dlq.FirstAttemptTime).TotalSeconds,
            dlq.RetryCount);

        await Task.CompletedTask;
    }
}

/// <summary>
/// Example: Store DLQ events in a repository for tracking and dashboard
/// </summary>
public interface IDeadLetterQueueRepository
{
    Task SaveAsync(DeadLetterQueueEvent dlqEvent);
    Task<IEnumerable<DeadLetterQueueEvent>> GetPendingAsync();
    Task<DeadLetterQueueEvent?> GetByCorrelationIdAsync(string correlationId);
    Task UpdateStatusAsync(string correlationId, string status, string? resolution = null);
}

/// <summary>
/// Example: Enhanced DLQ consumer with persistence and alerting
/// </summary>
public class EnhancedDeadLetterQueueConsumer : IConsumer<DeadLetterQueueEvent>
{
    private readonly ILogger<EnhancedDeadLetterQueueConsumer> _logger;
    private readonly IDeadLetterQueueRepository _repository;

    public EnhancedDeadLetterQueueConsumer(
        ILogger<EnhancedDeadLetterQueueConsumer> logger,
        IDeadLetterQueueRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public async Task Consume(ConsumeContext<DeadLetterQueueEvent> context)
    {
        var dlq = context.Message;

        try
        {
            // 1. Store in database for tracking
            await _repository.SaveAsync(dlq);

            // 2. Log critical information
            _logger.LogCritical(
                "💀 DLQ: {CorrelationId} | Booking: {BookingId} | Service: {Service} | Severity: {Severity}",
                dlq.CorrelationId,
                dlq.BookingId,
                dlq.FailedService,
                dlq.Severity);

            // 3. Trigger alerts and notifications
            await SendAlertsAsync(dlq);

            // 4. Create support ticket
            // await _ticketingService.CreateTicketAsync(dlq);

            // 5. Update monitoring dashboard
            // await _metricsService.IncrementDlqCounter(dlq.FailedService, dlq.Severity);

            _logger.LogInformation("DLQ event processed and stored successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process DLQ event {CorrelationId}", dlq.CorrelationId);
            throw; // Re-throw to allow MassTransit retry
        }
    }

    private async Task SendAlertsAsync(DeadLetterQueueEvent dlq)
    {
        // Example alert logic
        switch (dlq.Severity)
        {
            case "Critical":
                _logger.LogCritical("Sending critical alerts for {CorrelationId}", dlq.CorrelationId);
                // await _alertingService.SendPagerDutyAlert(dlq);
                // await _alertingService.SendSlackAlert("#critical-ops", dlq);
                // await _alertingService.SendEmailAlert("ops-team@example.com", dlq);
                break;

            case "High":
                _logger.LogError("Sending high priority alerts for {CorrelationId}", dlq.CorrelationId);
                // await _alertingService.SendSlackAlert("#alerts", dlq);
                // await _alertingService.SendEmailAlert("support@example.com", dlq);
                break;

            case "Medium":
                _logger.LogWarning("Sending medium priority notification for {CorrelationId}", dlq.CorrelationId);
                // await _alertingService.SendEmailAlert("support@example.com", dlq);
                break;
        }

        await Task.CompletedTask;
    }
}
