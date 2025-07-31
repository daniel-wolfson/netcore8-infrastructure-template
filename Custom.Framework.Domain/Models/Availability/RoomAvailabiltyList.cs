namespace Custom.Domain.Optima.Models.Availability
{
    public class RoomAvailabiltyList
    {
        public object VipCode { get; set; }
        public int LanguageID { get; set; }
        public int HotelID { get; set; }
        public string FromDate { get; set; }
        public string ToDate { get; set; }
        public int Adults { get; set; }
        public int Children { get; set; }
        public bool IsLocal { get; set; }
        public int CustomerID { get; set; }
        public int PriceCodeGroupID { get; set; }
        public int CachedCustomerID { get; set; }
        public int Infants { get; set; }
        public object ClerkKey { get; set; }

        public List<RoomPriceList> RoomPriceList { get; set; }

        public int RequestDescriptor { get; set; }
        public bool IsClubMemberResult { get; set; }
        public decimal DiscountPercent { get; set; }
        public bool IsExternalSystemResult { get; set; }
        public decimal ExternalSystemDiscountAmount { get; set; }
        public int ExternalSystemType { get; set; }
        public object ExternalSystemData { get; set; }
        public object PromoCode { get; set; }
        public DateTime StartProcess { get; set; }
        public DateTime EndProcess { get; set; }
    }
}