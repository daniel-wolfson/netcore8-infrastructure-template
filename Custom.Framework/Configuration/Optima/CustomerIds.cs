namespace Custom.Framework.Configuration.Optima
{
    /// <summary>
    /// Settings of customer (agent) id for each site
    /// </summary>
    public class CustomerIds
    {
        public int DesktopGuest { get; set; }
        public int DesktopSunClub { get; set; }
        public int MobileGuest { get; set; }
        public int MobileSunClub { get; set; }
        public int BasePrice { get; set; }
    }

    public class Customer
    {
        public bool IsDesktopGuest { get; }
        public bool IsDesktopSunClub { get; }
        public bool IsMobileGuest { get; }
        public bool IsMobileSunClub { get; }
        public bool IsBasePrice { get; }
        public bool IsMobile { get; }

        public string CustomerType { get; private set; }

        public Customer(
            bool isDesktopGuest,
            bool isDesktopSunClub,
            bool isMobileGuest,
            bool isMobileSunClub,
            bool isBasePrice,
            bool isMobile)
        {
            IsDesktopGuest = isDesktopGuest;
            IsDesktopSunClub = isDesktopSunClub;
            IsMobileGuest = isMobileGuest;
            IsMobileSunClub = isMobileSunClub;
            IsBasePrice = isBasePrice;
            IsMobile = isMobile;
            CustomerType = GetCustomerType();
        }

        public Customer(CustomerIds customers, int customerId)
            : this(
                customerId == customers.DesktopGuest,
                customerId == customers.DesktopSunClub,
                customerId == customers.MobileGuest,
                customerId == customers.MobileSunClub,
                customerId == customers.BasePrice,
                customerId == customers.MobileGuest || customerId == customers.MobileSunClub
            )
        {
        }
        public string GetCustomerType()
        {
            if (IsDesktopGuest) return CustomerType = "DesktopGuest";
            if (IsDesktopSunClub) return CustomerType = "DesktopSunClub";
            if (IsMobileGuest) return CustomerType = "MobileGuest";
            if (IsMobileSunClub) return CustomerType = "MobileSunClub";
            if (IsBasePrice) return CustomerType = "BasePrice";
            return "Unknown";
        }
    }
}
