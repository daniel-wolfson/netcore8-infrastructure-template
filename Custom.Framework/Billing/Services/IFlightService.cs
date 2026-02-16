using Custom.Framework.Billing.Models;

namespace Custom.Framework.Billing.Services
{
    public interface IFlightService
    {
        Task<bool> CancelFlightAsync(string reservationId);
        Task<FlightReservationResult> ReserveFlightAsync(TravelBookingDto dto);
    }
}