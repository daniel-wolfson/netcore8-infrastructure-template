namespace Custom.Domain.Optima.Models.Availability
{
    public class PolicyDay
    {
        public int hotelID { get; set; }
        public string cancellationPolicyCode { get; set; }
        public string daysType { get; set; }
        public int fromDay { get; set; }
        public int toDay { get; set; }
        public string chargeType { get; set; }
        public int charge { get; set; }
        public decimal chargePercent { get; set; }
        public string currencyCode { get; set; }
        public string cancelTime { get; set; }
        public bool delete { get; set; }
        public List<string> debugInfo { get; set; }
    }


}