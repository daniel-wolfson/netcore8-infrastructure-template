using Custom.Framework.Billing.Models;
using Microsoft.Extensions.Logging;

namespace Custom.Framework.Billing.Services;

/// <summary>
/// Service for hotel reservations (simulated)
/// </summary>
public class HotelService : IHotelService
{
    private readonly ILogger<HotelService> _logger;
    private readonly Random _random = new();

    public HotelService(ILogger<HotelService> logger)
    {
        _logger = logger;
    }

    public virtual Task<HotelReservationResult> ReserveHotelAsync(TravelBookingDto dto)
    {
        _logger.LogInformation("Reserving hotel: {HotelId} ({CheckIn} to {CheckOut})",
            dto.HotelId, dto.CheckInDate, dto.CheckOutDate);

        // Simulate processing delay
        Task.Delay(100).Wait();

        var result = new HotelReservationResult
        {
            ReservationId = $"HTL-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            ConfirmationCode = GenerateConfirmationCode(),
            Status = "confirmed",
            Amount = 875m
        };

        _logger.LogInformation("Hotel reserved: {ReservationId}", result.ReservationId);
        return Task.FromResult(result);
    }

    public virtual Task<bool> CancelHotelAsync(string reservationId)
    {
        _logger.LogInformation("Canceling hotel reservation: {ReservationId}", reservationId);
        Task.Delay(100).Wait();
        _logger.LogInformation("Hotel reservation canceled: {ReservationId}", reservationId);
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
