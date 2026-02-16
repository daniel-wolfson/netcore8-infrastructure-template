using Custom.Framework.Billing.Events;
using Custom.Framework.Billing.Models;
using Custom.Framework.Billing.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Custom.Framework.Billing.Sagas;

/// <summary>
/// Travel Booking Saga Orchestrator using MassTransit
/// 
/// Implements the Saga pattern for distributed transactions across multiple services:
/// Flight → Hotel → Car Rental → Payment
/// 
/// If any Travel-booking-saga-step fails, compensation transactions are executed in reverse order:
/// Cancel Car → Cancel Hotel → Cancel Flight
/// 
/// This ensures eventual consistency and handles failure scenarios gracefully.
/// </summary>
public class TravelBookingSaga
{
    private readonly ILogger<TravelBookingSaga> _logger;
    private readonly IFlightService _flightService;
    private readonly IHotelService _hotelService;
    private readonly ICarRentalService _carRentalService;
    private readonly IPublishEndpoint _publishEndpoint;

    public TravelBookingSaga(
        ILogger<TravelBookingSaga> logger,
        IFlightService flightService,
        IHotelService hotelService,
        ICarRentalService carRentalService,
        IPublishEndpoint publishEndpoint)
    {
        _logger = logger;
        _flightService = flightService;
        _hotelService = hotelService;
        _carRentalService = carRentalService;
        _publishEndpoint = publishEndpoint;
    }

    /// <summary>
    /// Execute the travel booking saga
    /// Returns a response with the booking status and details
    /// </summary>
    public async Task<TravelBookingResponseDto> ExecuteAsync(TravelBookingDto dto, CancellationToken cancellationToken = default)
    {
        var bookingId = GenerateBookingId();
        var sagaStartTime = DateTime.UtcNow;
        FlightReservationResult? flightReservation = null;
        HotelReservationResult? hotelReservation = null;
        CarRentalReservationResult? carRentalReservation = null;

        _logger.LogInformation("Starting Travel Booking Saga: {BookingId}", bookingId);
        _logger.LogInformation("User: {UserId}, Total Amount: ${TotalAmount}", dto.UserId, dto.TotalAmount);

        // Publish saga started event
        await _publishEndpoint.Publish(new TravelBookingCreatedEvent
        {
            BookingId = bookingId,
            UserId = dto.UserId,
            TotalAmount = dto.TotalAmount
        }, cancellationToken);

        try
        {
            // Travel-booking-saga-step 1: Reserve Flight
            flightReservation = await ReserveFlightAsync(dto);
            _logger.LogInformation("✓ Travel-booking-saga-step 1 Complete: Flight Reserved ({ReservationId})", flightReservation.ReservationId);

            // Travel-booking-saga-step 2: Reserve Hotel
            hotelReservation = await ReserveHotelAsync(dto);
            _logger.LogInformation("✓ Travel-booking-saga-step 2 Complete: Hotel Reserved ({ReservationId})", hotelReservation.ReservationId);

            // Travel-booking-saga-step 3: Reserve Car
            carRentalReservation = await ReserveCarAsync(dto);
            _logger.LogInformation("✓ Travel-booking-saga-step 3 Complete: Car Reserved ({ReservationId})", carRentalReservation.ReservationId);

            // Travel-booking-saga-step 4: Process Payment (simulated)
            await ProcessPaymentAsync(dto.UserId, dto.TotalAmount);
            _logger.LogInformation("✓ Travel-booking-saga-step 4 Complete: Payment Processed");

            // All Travel-booking-saga-steps successful
            _logger.LogInformation("✅ Travel Booking Saga Completed Successfully: {BookingId}", bookingId);

            return new TravelBookingResponseDto
            {
                BookingId = bookingId,
                FlightReservationId = flightReservation.ReservationId,
                HotelReservationId = hotelReservation.ReservationId,
                CarRentalReservationId = carRentalReservation.ReservationId,
                Status = "confirmed",
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            // Saga failed - execute compensating transactions
            var errorMessage = ex.Message;
            _logger.LogError(ex, "❌ Travel Booking Saga Failed: {ErrorMessage}", errorMessage);
            _logger.LogWarning("🔄 Starting Compensation Process...");

            await CompensateAsync(
                bookingId, 
                dto.UserId, 
                dto.TotalAmount, 
                flightReservation, 
                hotelReservation, 
                carRentalReservation, 
                sagaStartTime,
                cancellationToken);

            // Publish compensation event
            await _publishEndpoint.Publish(new TravelBookingCompensatedEvent
            {
                BookingId = bookingId,
                UserId = dto.UserId,
                ErrorMessage = errorMessage
            }, cancellationToken);

            return new TravelBookingResponseDto
            {
                BookingId = bookingId,
                FlightReservationId = flightReservation?.ReservationId,
                HotelReservationId = hotelReservation?.ReservationId,
                CarRentalReservationId = carRentalReservation?.ReservationId,
                Status = "compensated",
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Travel-booking-saga-step 1: Reserve Flight
    /// </summary>
    private async Task<FlightReservationResult> ReserveFlightAsync(TravelBookingDto dto)
    {
        _logger.LogInformation("Travel-booking-saga-step 1: Reserving Flight...");
        return await _flightService.ReserveFlightAsync(dto);
    }

    /// <summary>
    /// Travel-booking-saga-step 2: Reserve Hotel
    /// </summary>
    private async Task<HotelReservationResult> ReserveHotelAsync(TravelBookingDto dto)
    {
        _logger.LogInformation("Travel-booking-saga-step 2: Reserving Hotel...");
        return await _hotelService.ReserveHotelAsync(dto);
    }

    /// <summary>
    /// Travel-booking-saga-step 3: Reserve Car
    /// </summary>
    private async Task<CarRentalReservationResult> ReserveCarAsync(TravelBookingDto dto)
    {
        _logger.LogInformation("Travel-booking-saga-step 3: Reserving Car...");
        return await _carRentalService.ReserveCarAsync(dto);
    }

    /// <summary>
    /// Travel-booking-saga-step 4: Process Payment (simulated)
    /// </summary>
    private async Task ProcessPaymentAsync(string userId, decimal amount)
    {
        _logger.LogInformation("Travel-booking-saga-step 4: Processing Payment of ${Amount}...", amount);
        // Simulate payment processing
        await Task.Delay(100);
        _logger.LogInformation("Payment processed successfully");
    }

    /// <summary>
    /// Compensate (rollback) all successful Travel-booking-saga-steps in reverse order
    /// If compensation fails, sends event to Dead Letter Queue for manual intervention
    /// </summary>
    private async Task CompensateAsync(
        string bookingId,
        string userId,
        decimal totalAmount,
        FlightReservationResult? flightReservation,
        HotelReservationResult? hotelReservation,
        CarRentalReservationResult? carRentalReservation,
        DateTime sagaStartTime,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("🔄 Starting Compensation for Booking: {BookingId}", bookingId);

        var compensationFailures = new List<(string Service, string Action, Exception Error)>();

        // Compensate in reverse order: Car -> Hotel -> Flight

        // Travel-booking-saga-step 1: Compensate Car Rental
        if (carRentalReservation != null)
        {
            try
            {
                await _carRentalService.CancelCarAsync(carRentalReservation.ReservationId);
                _logger.LogInformation("✓ Compensated: Car rental canceled ({ReservationId})", carRentalReservation.ReservationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to compensate car rental");
                compensationFailures.Add(("CarRental", $"Cancel reservation {carRentalReservation.ReservationId}", ex));

                // Publish failure event
                await _publishEndpoint.Publish(new CompensationFailedEvent
                {
                    BookingId = bookingId,
                    Service = "CarRental",
                    ErrorMessage = ex.Message
                }, cancellationToken);

                // Send to Dead Letter Queue
                await SendToDeadLetterQueueAsync(
                    bookingId,
                    userId,
                    totalAmount,
                    "CarRental",
                    $"Cancel reservation {carRentalReservation.ReservationId}",
                    ex,
                    flightReservation,
                    hotelReservation,
                    carRentalReservation,
                    sagaStartTime,
                    cancellationToken);
            }
        }

        // Travel-booking-saga-step 2: Compensate Hotel
        if (hotelReservation != null)
        {
            try
            {
                await _hotelService.CancelHotelAsync(hotelReservation.ReservationId);
                _logger.LogInformation("✓ Compensated: Hotel canceled ({ReservationId})", hotelReservation.ReservationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to compensate hotel");
                compensationFailures.Add(("Hotel", $"Cancel reservation {hotelReservation.ReservationId}", ex));

                // Publish failure event
                await _publishEndpoint.Publish(new CompensationFailedEvent
                {
                    BookingId = bookingId,
                    Service = "Hotel",
                    ErrorMessage = ex.Message
                }, cancellationToken);

                // Send to Dead Letter Queue
                await SendToDeadLetterQueueAsync(
                    bookingId,
                    userId,
                    totalAmount,
                    "Hotel",
                    $"Cancel reservation {hotelReservation.ReservationId}",
                    ex,
                    flightReservation,
                    hotelReservation,
                    carRentalReservation,
                    sagaStartTime,
                    cancellationToken);
            }
        }

        // Travel-booking-saga-step 3: Compensate Flight
        if (flightReservation != null)
        {
            try
            {
                await _flightService.CancelFlightAsync(flightReservation.ReservationId);
                _logger.LogInformation("✓ Compensated: Flight canceled ({ReservationId})", flightReservation.ReservationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to compensate flight");
                compensationFailures.Add(("Flight", $"Cancel reservation {flightReservation.ReservationId}", ex));

                // Publish failure event
                await _publishEndpoint.Publish(new CompensationFailedEvent
                {
                    BookingId = bookingId,
                    Service = "Flight",
                    ErrorMessage = ex.Message
                }, cancellationToken);

                // Send to Dead Letter Queue
                await SendToDeadLetterQueueAsync(
                    bookingId,
                    userId,
                    totalAmount,
                    "Flight",
                    $"Cancel reservation {flightReservation.ReservationId}",
                    ex,
                    flightReservation,
                    hotelReservation,
                    carRentalReservation,
                    sagaStartTime,
                    cancellationToken);
            }
        }

        // Log compensation summary
        if (compensationFailures.Any())
        {
            _logger.LogError(
                "⚠️ Compensation completed with {FailureCount} failure(s) for Booking: {BookingId}. Manual intervention required.",
                compensationFailures.Count,
                bookingId);
        }
        else
        {
            _logger.LogInformation("✅ Compensation Complete for Booking: {BookingId}", bookingId);
        }
    }

    /// <summary>
    /// Send failed compensation details to Dead Letter Queue for manual intervention
    /// </summary>
    private async Task SendToDeadLetterQueueAsync(
        string bookingId,
        string userId,
        decimal totalAmount,
        string failedService,
        string compensationAction,
        Exception exception,
        FlightReservationResult? flightReservation,
        HotelReservationResult? hotelReservation,
        CarRentalReservationResult? carRentalReservation,
        DateTime sagaStartTime,
        CancellationToken cancellationToken)
    {
        try
        {
            var dlqEvent = new DeadLetterQueueEvent
            {
                CorrelationId = Guid.NewGuid().ToString(),
                BookingId = bookingId,
                UserId = userId,
                FailedService = failedService,
                ErrorMessage = exception.Message,
                StackTrace = exception.StackTrace ?? "No stack trace available",
                CompensationAction = compensationAction,
                FlightReservationId = flightReservation?.ReservationId,
                HotelReservationId = hotelReservation?.ReservationId,
                CarRentalReservationId = carRentalReservation?.ReservationId,
                TotalAmount = totalAmount,
                RetryCount = 0, // This is the first attempt, can be enhanced with retry tracking
                FirstAttemptTime = sagaStartTime,
                FailedAttemptTime = DateTime.UtcNow,
                Severity = DetermineSeverity(failedService),
                AdditionalContext = new Dictionary<string, object>
                {
                    { "ExceptionType", exception.GetType().Name },
                    { "InnerException", exception.InnerException?.Message ?? "None" },
                    { "SagaDuration", (DateTime.UtcNow - sagaStartTime).TotalSeconds },
                    { "CompensationTravel-booking-saga-step", failedService }
                }
            };

            // Publish to Dead Letter Queue
            await _publishEndpoint.Publish(dlqEvent, cancellationToken);

            _logger.LogCritical(
                "💀 Dead Letter Queue Event Published | CorrelationId: {CorrelationId} | BookingId: {BookingId} | Service: {Service} | Severity: {Severity}",
                dlqEvent.CorrelationId,
                bookingId,
                failedService,
                dlqEvent.Severity);
        }
        catch (Exception dlqEx)
        {
            // Last resort: if we can't even send to DLQ, log it critically
            _logger.LogCritical(
                dlqEx,
                "🚨 CRITICAL: Failed to send to Dead Letter Queue | BookingId: {BookingId} | Service: {Service}",
                bookingId,
                failedService);
        }
    }

    /// <summary>
    /// Determine severity based on which service failed compensation
    /// </summary>
    private string DetermineSeverity(string failedService)
    {
        // Flight cancellation failures are most critical (usually most expensive)
        // Car rental failures are least critical
        return failedService switch
        {
            "Flight" => "Critical",
            "Hotel" => "High",
            "CarRental" => "Medium",
            _ => "High"
        };
    }

    private string GenerateBookingId()
    {
        return $"TRV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
    }
}
