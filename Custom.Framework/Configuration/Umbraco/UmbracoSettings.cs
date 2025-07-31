using Custom.Domain.Optima.Models;
using Custom.Domain.Optima.Models.Umbraco;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Framework.Configuration.Umbraco
{
    public class UmbracoSettings
    {
        //[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public DateTime AvailableStartDate { get; set; }
        public DateTime AvailableEndDate { get; set; }
        public bool DisableFlightsForSite { get; set; }
        public int Channel { get; set; }
        public bool IsWl { get; set; }
        public string UmbracoLinkToNop { get; set; } = string.Empty;
        public string UmbracoSunClubLinkToNop { get; set; } = string.Empty;
        public int MaxAllowedNights { get; set; }
        public List<string>? AllowedFlightHotels { get; set; }
        public List<string>? ExcludedHotels { get; set; }
        [NotMapped]
        public List<HotelRoomFilter> RoomsFilter { get; set; } = [];
        [NotMapped]
        public List<AlternativeHotelsOption> AlternativeHotelsOptions { get; set; } = [];
        [NotMapped]
        public List<RoomSeparationOptions> RoomSeparateCombinations { get; set; } = [];
        [NotMapped]
        public List<AvailableProperties> AvailableHotelsAndRooms { get; set; } = [];
        [NotMapped]
        public List<ConnectingDoorSettings> ConnectingDoorSettings { get; set; } = [];
    }
}