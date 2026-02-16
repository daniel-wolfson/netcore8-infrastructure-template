using Custom.Framework.Billing.Models;
using Microsoft.Extensions.Logging;

namespace Custom.Framework.Billing.Services;

/// <summary>
/// Service for flight reservations (simulated)
/// </summary>
public class FlightService : IFlightService
{
    private readonly ILogger<FlightService> _logger;
    private readonly Random _random = new();

    public FlightService(ILogger<FlightService> logger)
    {
        _logger = logger;
    }

    public virtual Task<FlightReservationResult> ReserveFlightAsync(TravelBookingDto dto)
    {
        _logger.LogInformation("Reserving flight: {Origin} → {Destination}", dto.FlightOrigin, dto.FlightDestination);

        // Simulate processing delay
        Task.Delay(100).Wait();

        var result = new FlightReservationResult
        {
            ReservationId = $"FLT-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            ConfirmationCode = GenerateConfirmationCode(),
            Status = "confirmed",
            Amount = 1000m
        };

        _logger.LogInformation("Flight reserved: {ReservationId}", result.ReservationId);
        return Task.FromResult(result);
    }

    public virtual Task<bool> CancelFlightAsync(string reservationId)
    {
        _logger.LogInformation("Canceling flight reservation: {ReservationId}", reservationId);
        Task.Delay(100).Wait();
        _logger.LogInformation("Flight reservation canceled: {ReservationId}", reservationId);
        return Task.FromResult(true);
    }

    private string GenerateConfirmationCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Range(0, 6)
            .Select(_ => chars[_random.Next(chars.Length)])
            .ToArray());
    }
}
