using Custom.Domain.Optima.Dto;
using Custom.Domain.Optima.Models.Enums;
using Custom.Domain.Optima.Models.Validation;
using System.Text.Json.Serialization;

namespace Custom.Domain.Optima.Models
{
    /// <summary>
    /// Every pacakge represents a room (or splited rooms) option in search result page
    /// </summary>
    public class PackageResult
    {
        public string PackageId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public List<RoomResult> Rooms { get; set; } = [];
        public List<Sale> Sales { get; set; }

        public string HotelName { get; set; }
        public string HotelCode { get; set; }
        public int HotelId { get; set; }

        public int PackageOccupancyCode { get; set; }
        public string? RoomCodes { get; set; }
        public bool IsConnectedDoor { get; set; }
        public bool IsSplitedRoomsPackage => Rooms.Count > 1;

        [JsonIgnore]
        public decimal ILSPrice { get; set; }
        [JsonIgnore]
        public string SiteUrl { get; set; }
        [JsonIgnore]
        public decimal? GoogleMapLongitude { get; set; }
        [JsonIgnore]
        public decimal? GoolgeMapLatitude { get; set; }
        [JsonIgnore]
        public List<OrderDetailsSummary> OrderDetailsSummary { get; set; }
        [JsonIgnore]
        public ESearchResultType ResultType { get; set; }
        [JsonIgnore]
        public string CurrencyCode { get; set; }
        [JsonIgnore]
        public object PackageFlightsDetails { get; set; }

        public ApplyFilter DebugMode { get; set; }
    }
}
