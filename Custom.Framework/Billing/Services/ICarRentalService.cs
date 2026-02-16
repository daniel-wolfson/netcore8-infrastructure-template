using Custom.Framework.Billing.Models;

namespace Custom.Framework.Billing.Services
{
    public interface ICarRentalService
    {
        Task<bool> CancelCarAsync(string reservationId);
        Task<CarRentalReservationResult> ReserveCarAsync(TravelBookingDto dto);
    }
}