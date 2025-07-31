namespace Custom.Domain.Optima.Models.Availability
{
    public class RoomPriceList
    {
        public bool IsUpdated { get; set; }
        public bool IsOverRideClubDiscount { get; set; }
        public DateTime Creation { get; set; }
        public string RoomCategory { get; set; }
        public string PriceCode { get; set; }
        public string PlanCode { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal TotalPriceNoTax { get; set; }
        public decimal Points { get; set; }
        public bool IsPointsCanBeUsed { get; set; }
        public decimal ExpectedPoints { get; set; }
        public string CurrencyCode { get; set; }
        public int CancelDays { get; set; }
        public string PolicyCode { get; set; }
        public object CancellationPolicyCode { get; set; }
        public object AllocationCode { get; set; }
        public int ErrorID { get; set; }
        public string ErrorText { get; set; }
        public int ErrorSortOrder { get; set; }
        public string SessionID { get; set; }
        public DateTime SessionCreation { get; set; }
        public bool MustProvidePayment { get; set; }
        public List<PricePerDayList> PricePerDayList { get; set; }
        public int SessionChanges { get; set; }
        public object? PolicyInfo { get; set; }
        public List<CategoriesList> CategoriesList { get; set; }
        public CategoriesMessage CategoriesMessage { get; set; }
        public object DebugInfo { get; set; }
    }
}       