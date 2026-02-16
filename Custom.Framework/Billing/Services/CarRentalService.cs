using Custom.Framework.Billing.Models;
using Microsoft.Extensions.Logging;

namespace Custom.Framework.Billing.Services;

/// <summary>
/// Service for car rental reservations (simulated)
/// </summary>
public class CarRentalService : ICarRentalService
{
    private readonly ILogger<CarRentalService> _logger;
    private readonly Random _random = new();

    public CarRentalService(ILogger<CarRentalService> logger)
    {
        _logger = logger;
    }

    public virtual Task<CarRentalReservationResult> ReserveCarAsync(TravelBookingDto dto)
    {
        _logger.LogInformation("Reserving car: {PickupLocation} ({PickupDate} to {DropoffDate})",
            dto.CarPickupLocation, dto.CarPickupDate, dto.CarDropoffDate);

        // Simulate processing delay
        Task.Delay(100).Wait();

        var result = new CarRentalReservationResult
        {
            ReservationId = $"CAR-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            ConfirmationCode = GenerateConfirmationCode(),
            Status = "confirmed",
            Amount = 625m
        };

        _logger.LogInformation("Car reserved: {ReservationId}", result.ReservationId);
        return Task.FromResult(result);
    }

    public virtual Task<bool> CancelCarAsync(string reservationId)
    {
        _logger.LogInformation("Canceling car rental reservation: {ReservationId}", reservationId);
        Task.Delay(100).Wait();
        _logger.LogInformation("Car rental reservation canceled: {ReservationId}", reservationId);
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
