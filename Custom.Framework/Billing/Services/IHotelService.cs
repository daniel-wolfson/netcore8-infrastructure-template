using Custom.Framework.Billing.Models;

namespace Custom.Framework.Billing.Services
{
    public interface IHotelService
    {
        Task<bool> CancelHotelAsync(string reservationId);
        Task<HotelReservationResult> ReserveHotelAsync(TravelBookingDto dto);
    }
}