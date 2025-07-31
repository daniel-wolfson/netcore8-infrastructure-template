using Custom.Domain.Optima.Models.Enums;

namespace Custom.Domain.Optima.Models
{
    public class RoomResult
    {
        public string RoomCode { get; set; }

        /// <summary>
        /// For packages that contain multiple rooms combination, 
        /// get each room separately Price details, for later reservation process.
        /// </summary>
        public string SingleRoomPackageId { get; set; }

        /// <summary>
        /// If the current room is part of room combination (split),
        /// this occupancy code specifies the original room code, for later reservation process.
        /// </summary>
        public int OriginalOccupancyCode { get; set; }

        public EAvailabilityRequestType RequestType { get; set; }

        public List<int>? MatchingOccupancyCombinations { get; set; }

        public object RoomDetails { get; set; }

        public int AvailableUnits { get; set; }

        public Occupancy Occupancy { get; set; }

        /// <summary>
        /// Refers to a single room package. Will be the same as the entire PackageResult Sales property, as long as it's a single room package.
        /// In combined packages, it will refer to one of the two combined rooms.
        /// </summary>
        public List<SingleRoomPriceDetails> SingleRoomPriceDetails { get; set; }

        public bool IsConnectingRoom { get; set; }
    }
}
