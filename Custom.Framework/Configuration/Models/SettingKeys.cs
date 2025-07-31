using Custom.Domain.Optima.Models.Customer;
using Custom.Domain.Optima.Models.Main;
using Custom.Domain.Optima.Models.Umbraco;
using Custom.Framework.Configuration.Optima;
using Custom.Framework.Configuration.Umbraco;
using System.ComponentModel.DataAnnotations;

// ReSharper disable PropertyNotResolved
namespace Custom.Framework.Configuration.Models
{
    public enum SettingKeys
    {
        [Display(ResourceType = typeof(string), GroupName = ProviderTypes.Unknown)]
        Unknown,

        [Display(ResourceType = typeof(object), GroupName = ProviderTypes.Unknown)]
        Default,

        [Display(ResourceType = typeof(ApiSettings), GroupName = ProviderTypes.Unknown)]
        ApiSettings,

        [Display(ResourceType = typeof(object), GroupName = ProviderTypes.Unknown)]
        StaticData,

        [Display(ResourceType = typeof(OptimaSettings), GroupName = ProviderTypes.Umbraco)]
        OptimaSettings,

        [Display(ResourceType = typeof(PmsSettings), GroupName = ProviderTypes.Umbraco)]
        PmsSettings,

        [Display(ResourceType = typeof(List<ClerkData>), GroupName = ProviderTypes.Optima)]
        Clerks,

        [Display(ResourceType = typeof(List<PolicyData>), GroupName = ProviderTypes.Optima)]
        Policy,

        [Display(ResourceType = typeof(List<RegionData>), GroupName = ProviderTypes.Optima)]
        Regions,

        [Display(ResourceType = typeof(List<HotelData>), GroupName = ProviderTypes.Optima)]
        Hotels,

        [Display(ResourceType = typeof(List<RoomData>), GroupName = ProviderTypes.Umbraco)]
        Rooms,

        [Display(ResourceType = typeof(List<PlanData>), GroupName = ProviderTypes.Optima)]
        Plans,

        [Display(ResourceType = typeof(List<PriceCodeData>), GroupName = ProviderTypes.Optima)]
        PriceCodes,

        [Display(ResourceType = typeof(List<PriceGroupData>), GroupName = ProviderTypes.Optima)]
        PriceGroups,

        [Display(ResourceType = typeof(List<PriceCodeCategoryData>), GroupName = ProviderTypes.Optima)]
        PriceCodeCategories,

        [Display(ResourceType = typeof(List<PriceCodeCategoryTranslationsData>), GroupName = ProviderTypes.Optima)]
        PriceCodeCategoriesTranslations,

        [Display(ResourceType = typeof(List<PriceCodeCategoryLinkData>), GroupName = ProviderTypes.Optima)]
        PriceCodeCategoryLinks,

        [Display(ResourceType = typeof(List<RoomTranslationsData>), GroupName = ProviderTypes.Unknown)]
        RoomTranslations,

        [Display(ResourceType = typeof(List<PriceCodeTranslationsData>), GroupName = ProviderTypes.Optima)]
        PriceCodeTranslations,

        [Display(ResourceType = typeof(List<PackageTranslationsData>), GroupName = ProviderTypes.Optima)]
        PackageTranslations,

        [Display(ResourceType = typeof(List<UmbracoSettings>), GroupName = ProviderTypes.Umbraco)]
        UmbracoSettings,

        [Display(ResourceType = typeof(List<UmbracoSettings>), GroupName = ProviderTypes.Umbraco)]
        UmbracoSearchSettings,

        [Display(ResourceType = typeof(List<PackageShowData>), GroupName = ProviderTypes.Optima)]
        PackageShow,

        [Display(ResourceType = typeof(object), GroupName = ProviderTypes.AzureStorage)]
        RoomSpecialRequestsCodes,

        [Display(ResourceType = typeof(object), GroupName = ProviderTypes.Unknown)]
        NopSearchSettings,

        [Display(ResourceType = typeof(object), GroupName = ProviderTypes.Unknown)]
        AllRatePlanCodes,

        [Display(ResourceType = typeof(object), GroupName = ProviderTypes.Unknown)]
        DynamicFlightSettings,

        [Display(ResourceType = typeof(object), GroupName = ProviderTypes.Unknown)]
        Sales,

        [Display(ResourceType = typeof(object), GroupName = ProviderTypes.Unknown)]
        SalesPromotion,

        [Display(ResourceType = typeof(object), GroupName = ProviderTypes.Unknown)]
        ReservationSettings,

        [Display(ResourceType = typeof(object), GroupName = ProviderTypes.Unknown)]
        SunClubSettings,

        [Display(ResourceType = typeof(object), GroupName = ProviderTypes.Unknown)]
        CancelReservationBccAddresses,

        [Display(ResourceType = typeof(object), GroupName = ProviderTypes.Unknown)]
        OfflinePricesForAllRooms,

        [Display(ResourceType = typeof(string), GroupName = ProviderTypes.Unknown)]
        ReservationTerms,

        [Display(ResourceType = typeof(string), GroupName = ProviderTypes.Unknown)]
        ConnectingDoorReservationEmailAddresses,

        [Display(ResourceType = typeof(int), GroupName = ProviderTypes.Unknown)]
        ReloadInterval,

        [Display(ResourceType = typeof(string), GroupName = ProviderTypes.Unknown)]
        ReloadOnChange,

        [Display(ResourceType = typeof(int), GroupName = ProviderTypes.Unknown)]
        ReloadTimeout,

        [Display(ResourceType = typeof(string), GroupName = ProviderTypes.Unknown)]
        SourceConnectionKey,

        [Display(ResourceType = typeof(List<string>), GroupName = ProviderTypes.StaticList)]
        HotelCodes,
        
        [Display(ResourceType = typeof(List<int>), GroupName = ProviderTypes.StaticList)]
        HotelIds,

        [Display(ResourceType = typeof(List<int>), GroupName = ProviderTypes.Unknown)]
        Languages,

        [Display(ResourceType = typeof(string), GroupName = ProviderTypes.String)]
        Path,

        [Display(ResourceType = typeof(int), GroupName = ProviderTypes.Number)]
        Order,

        [Display(ResourceType = typeof(List<object>), GroupName = ProviderTypes.Unknown)]
        RoomsBestPrices,

        [Display(ResourceType = typeof(List<CurrencyRate>), GroupName = ProviderTypes.Dal)]
        CurrencyRates,

        [Display(ResourceType = typeof(List<object>), GroupName = ProviderTypes.Unknown)]
        SunClubRegisterEmailTemplate,

        [Display(ResourceType = typeof(string), GroupName = ProviderTypes.Nop)]
        NopSettings,

        [Display(ResourceType = typeof(List<int>), GroupName = ProviderTypes.StaticList)]
        CustomerIds,
        
        [Display(ResourceType = typeof(List<int>), GroupName = ProviderTypes.StaticList)]
        RootNodeIds
    }
}