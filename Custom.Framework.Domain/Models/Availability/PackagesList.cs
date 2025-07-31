namespace Custom.Domain.Optima.Models.Availability
{
    public class PackagesList
    {
        // Ids
        public int HotelID { get; set; }
        public int PackageID { get; set; }
        public int ParentID { get; set; }
        public int CustomerID { get; set; }
        public string SessionID { get; set; }
        public int LanguageID { get; set; }
        public int PackageGroupID { get; set; }
        public int CachedCustomerID { get; set; }

        // FilterName and descripton
        public string Name { get; set; }
        public string Description { get; set; }
        public string ShortDescription { get; set; }

        // Dates and days
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime Creation { get; set; }
        public DateTime SessionCreation { get; set; }
        public int CancelDays { get; set; }

        // Occupancy
        public int Adults { get; set; }
        public int Children { get; set; }
        public int Infants { get; set; }
        public int OccupancyCode => Adults * 100 + Children * 10 + Infants;
        public int AvailableRooms { get; set; }

        // Prices
        public string PriceCode { get; set; }
        public decimal BasePrice { get; set; }
        public decimal BasePriceNoTax { get; set; }

        public decimal TotalPrice { get; set; }
        public decimal TotalPriceNoTax { get; set; }
        
        /// <summary> Calculated Price from PricePerDayList </summary>
        public decimal FinalPricePerDay { get; set; }
        
        public List<PricePerDayList> PricePerDayList { get; set; }

        public decimal OldPrice { get; set; }
        public decimal OldPriceNoTax { get; set; }
        public decimal PriceFirstDay { get; set; }
        
        public decimal TotalOldPrice { get; set; }
        public decimal TotalOldPriceNoTax { get; set; }
        public string CurrencyCode { get; set; }
        public string CurrencyCodeSource { get; set; }
        public decimal DiscountPercent { get; set; }
        
        // Room Category and Plan
        public string RoomCategory { get; set; }
        public string RoomCode { get; set; }
        public string PlanCode { get; set; }
        public string PlanCodeSource { get; set; }

        // Custom (not optima source) props
        public int RequestType { get; set; }        // added from umbraco settings, for example EAvailabilityRequestType.SplitRooms
        public bool IsConnectedDoor { get; set; }   // added from umbraco settings, AvailableHotelsAndRooms => ConnectingDoorRooms
        public bool IsPackage { get; set; } = true;  // IsPackage = true for roomAvailability of AvailabilityPrices response


        // Codes
        public string ReasonCode { get; set; }
        public string ClubCode { get; set; }
        public string VipCode { get; set; }
        public string PolicyCode { get; set; }
        public string CancellationPolicyCode { get; set; }
        public string AllocationCode { get; set; }


        // Errors
        public int ErrorID { get; set; }
        public string ErrorText { get; set; }
        public string ErrorParam { get; set; }
        public int ErrorSortOrder { get; set; }

        // Flags
        public bool IsLocal { get; set; }
        public bool IsUpdated { get; set; }
        public bool IsClubMemberResult { get; set; }
        public bool IsExternalSystemResult { get; set; }
        public bool IsDerivedPackage { get; set; }
        public bool IsToConsiderAgentsDiscount { get; set; }
        public bool IsPreferredPackage { get; set; }
        public bool IsIgonreAgentDiscountForPackages { get; set; }
        public bool IsPointsCanBeUsed { get; set; }
        public bool IsGuaranteed { get; set; }

        // Others
        public int DisplayOrder { get; set; }
        public int AgentClubDiscFlag { get; set; }
        public int SessionChanges { get; set; }
        public decimal ExternalSystemDiscountAmount { get; set; }
        public int ExternalSystemType { get; set; }
        public string ExternalSystemData { get; set; }
        public decimal Points { get; set; }
        public decimal ExpectedPoints { get; set; }
        public List<object>? PolicyInfo { get; set; }
        public List<CategoryList> CategoryList { get; set; }
        public List<string> DebugInfo { get; set; }
    }
}