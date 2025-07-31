using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Umbraco;
using System.ComponentModel.DataAnnotations;

namespace Custom.Framework.Cache
{
    public enum CacheKeys
    {
        [Display(ResourceType = typeof(Dictionary<string, UmbracoSettings>))]
        UmbracoSettings,

        [Display(ResourceType = typeof(object))]
        RoomSpecialRequestsCodes,

        [Display(ResourceType = typeof(object))]
        NopSearchSettings,

        [Display(ResourceType = typeof(object))]
        AllRatePlanCodes,

        [Display(ResourceType = typeof(object))]
        DynamicFlightSettings,

        [Display(ResourceType = typeof(object))]
        Sales,

        [Display(ResourceType = typeof(object))]
        SalesPromotion,

        [Display(ResourceType = typeof(object))]
        ReservationSettings,

        [Display(ResourceType = typeof(object))]
        SunClubSettings,

        [Display(ResourceType = typeof(object))]
        CancelReservationBccAddresses,

        [Display(ResourceType = typeof(object))]
        OfflinePricesForAllRooms,

        [Display(ResourceType = typeof(object))]
        ReservationTerms,

        [Display(ResourceType = typeof(object))]
        ConnectingDoorReservationEmailAddresses,

        [Display(ResourceType = typeof(ApiSettings))]
        ApiSettings,

        [Display(ResourceType = typeof(object))]
        OptimaSettings,
        StaticData,

        Regions,
        Hotels,
        Rooms,
        Plans,
        PriceCodes,
        PriceCodesCategoriesData,
        PriceCodeCategoryLink,
        PriceCodeTranslations,
        PackageTranslations,
        RoomTranslations,
        CurrencyRate,
        PackageShow
    }
}
