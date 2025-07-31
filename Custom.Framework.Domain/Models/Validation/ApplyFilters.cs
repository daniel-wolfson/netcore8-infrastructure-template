using System.ComponentModel.DataAnnotations;

namespace Custom.Domain.Optima.Models.Validation
{
    public enum ApplyFilters
    {
        [Display(Name = "None", Description = "Dummy empty")]
        None,

        [Display(
            Name = "Filter-IsPlanCodeValid", 
            Description = "remove all packages not matched two first symbols")]
        FilterByIsPlanCodeValid,

        [Display(
            Name = "Filter-RoomCodes", 
            Description = "remove all packages not mached to umbraco RoomCodes")]
        FilterByRoomCodes,
        
        [Display(
            Name = "Filter-EmptyResults", 
            Description = "remove all empty sales")]
        FilterEmptySales,

        [Display(
            Name = "Filter-EmptyBoardbases",
            Description = "remove all empty boardbases")]
        FilterEmptyBoardbases,

        [Display(
            Name = "Filter-Hotel-ConnectingDoor-Rooms-NotContains-RoomCode", 
            Description = "remove all by hotelConnectingDoor rooms not contains umbraco roomCode")]
        FilterByHotelConnectingDoorRoomsNotContainsRoomCode,

        [Display(
            Name = "Validation-RoomCode-TotalDays-Less-Than2Days", 
            Description = "Verifying that roomCode totalDays less than 2 days")]
        ValidationRoomCodeTotalDaysLessThan2Days,
        
        [Display(
            Name = "Validation-HotelConnectingDoorRoomsEmpty", 
            Description = "Verifying that HotelConnectingDoorRooms is not empty")]
        FilterByHotelConnectingDoorRoomsEmpty,

        [Display(
            Name = "Filter-MinimumChildren-ConnectingDoor-RoomCount", 
            Description = "remove all packages by MinimumChildren-ConnectingDoor-RoomCount")]
        FilterByMinimumChildrenConnectingDoorRoomCount,

        [Display(
            Name = "Filter-ConnectingDoorSettings-AvailableUnits", 
            Description = "remove all packages by ConnectingDoorSettings AvailableUnits")]
        FilterByConnectingDoorSettingsAvailableUnits,

        [Display(
            Name = "Filter-FilterByAllowedRoomOccupancy", 
            Description = "remove all packages by Umbraco settings: RoomCodes")]
        FilterByAllowedRoomOccupancy,

        [Display(
            Name = "Filter-FilterByOptimaValidator", 
            Description = "remove all packages by OptimaValidator: PlanCodes, PricePerDays")]
        FilterByOptimaValidator,

            [Display(
            Name = "Filter-FilterFitPriceCodes",
            Description = "remove all packages based on an FIT price code, and keeps only the cheapest package of FIT-group price code.")]
        FilterFitPriceCodes
    }
}