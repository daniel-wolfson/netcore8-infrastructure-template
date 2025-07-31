namespace Custom.Framework.Configuration.Nop
{
    public class NopConfig : ConfigData
    {
        public string NopSearchSettings { get; set; }
        public string AllRatePlanCodes { get; set; }
        public string DynamicFlightSettings { get; set; }
        public string Sales { get; set; }
        public string SalesPromotion { get; set; }
        public string ReservationSettings { get; set; }
        public string OfflinePricesForAllRooms { get; set; }
        public string ReservationTerms { get; set; }
        public string SunClubRegisterEmailTemplate { get; set; }

        //public string this[string propertyName]
        //{
        //    get
        //    {
        //        return propertyName switch
        //        {
        //            nameof(NopApiUrl) => NopApiUrl,
        //            nameof(NopSearchSettings) => NopSearchSettings,
        //            nameof(AllRatePlanCodes) => AllRatePlanCodes,
        //            nameof(DynamicFlightSettings) => DynamicFlightSettings,
        //            nameof(Sales) => Sales,
        //            nameof(SalesPromotion) => SalesPromotion,
        //            nameof(ReservationSettings) => ReservationSettings,
        //            nameof(OfflinePricesForAllRooms) => OfflinePricesForAllRooms,
        //            nameof(ReservationTerms) => ReservationTerms,
        //            nameof(SunClubRegisterEmailTemplate) => SunClubRegisterEmailTemplate,
        //            _ => base[propertyName] // Access base class properties
        //        };
        //    }
        //    set
        //    {
        //        switch (propertyName)
        //        {
        //            case nameof(NopApiUrl):
        //                NopApiUrl = value;
        //                break;
        //            case nameof(NopSearchSettings):
        //                NopSearchSettings = value;
        //                break;
        //            case nameof(AllRatePlanCodes):
        //                AllRatePlanCodes = value;
        //                break;
        //            case nameof(DynamicFlightSettings):
        //                DynamicFlightSettings = value;
        //                break;
        //            case nameof(Sales):
        //                Sales = value;
        //                break;
        //            case nameof(SalesPromotion):
        //                SalesPromotion = value;
        //                break;
        //            case nameof(ReservationSettings):
        //                ReservationSettings = value;
        //                break;
        //            case nameof(OfflinePricesForAllRooms):
        //                OfflinePricesForAllRooms = value;
        //                break;
        //            case nameof(ReservationTerms):
        //                ReservationTerms = value;
        //                break;
        //            case nameof(SunClubRegisterEmailTemplate):
        //                SunClubRegisterEmailTemplate = value;
        //                break;
        //            default:
        //                base[propertyName] = value; // Set base class properties
        //                break;
        //        }
        //    }
        //}
    }
}