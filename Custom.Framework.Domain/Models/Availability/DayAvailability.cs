namespace Custom.Domain.Optima.Models.Availability;

public class DayAvailability
{
    public DateTime Date { get; set; }
    public bool Available { get; set; }
    public int MinLOS { get; set; }
    public int MaxLOS { get; set; }
    public string RoomCategory { get; set; }
    public int RoomsAvailableForSale { get; set; }
    public bool ClosedForArrival { get; set; }
    public bool ClosedForDeparture { get; set; }
}