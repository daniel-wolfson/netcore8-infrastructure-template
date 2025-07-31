using Custom.Domain.Optima.Models;
using Custom.Domain.Optima.Models.Availability;
using Custom.Domain.Optima.Models.Enums;
using Custom.Domain.Optima.Models.Main;
using Custom.Domain.Optima.Models.Umbraco;
using Custom.Framework.Configuration.Optima;
using Custom.Framework.Configuration.Umbraco;
using Custom.Framework.Models.Base;

namespace Custom.Framework.Contracts
{
    public interface IApiOptimaDataMapper
    {
        int HomeRootNodeId { get; }
        int HotelID { get; }
        List<int> HotelIDs { get; }
        int LanguageID { get; set; }
        Dictionary<string, List<string>> ErrorList { get; set; }

        //OptimaContext ApiSettingsContext { get; }
        CustomerIds CustomerIdDataSet { get; }
        IEnumerable<HotelData> HotelDataSet { get; }
        OptimaSettings OptimaDataSet { get; }
        IEnumerable<PackageTranslationsData> PackageTranslationDataSet { get; }
        IEnumerable<PlanData> PlanDataSet { get; }
        IEnumerable<PriceCodeCategoryLinkData> PriceCodeCategoryLinkDataSet { get; }
        IEnumerable<PriceCodeData> PriceCodeDataSet { get; }
        IEnumerable<PriceCodeCategoryData> PriceCodesCategoriesDataset { get; }
        IEnumerable<PriceCodeTranslationsData> PriceCodeTranslationsDataSet { get; }
        IEnumerable<RoomData> RoomDataSet { get; }
        IEnumerable<UmbracoSettings> UmbracoDataSet { get; }
        UmbracoSettings UmbracoSearchDataSet { get; }

        string GetCurrencyCode(string? currencyCode);
        Customer GetCustomerIdInfo(int customerId);
        Occupancy GetOccupancy(PackagesList package);
        int GetOccupancyCode(PackagesList package);

        string GetRegionName(string hotelCode, int customerId);
        string GetRoomCode(int hotelID, string roomCategory);
        Task<TResult> GetServiceResult<TRequest, TResult>(string apiHttpClientName, string path, TRequest request)
            where TRequest : OptimaRequest, new()
            where TResult : IOptimaResult, new();
        string MapToBoardbase(PlanData plan);
        string MapToBoardbase(string plan);
        EChannel MapToChannel(int channel, EChannel? channelDefault = EChannel.WHENIS);
        int MapToCustomerId(int rootNodeId, bool isMobile, bool isSunClub, bool IsBasePrice = false);
        int? MapToGuestId(string? guestId);
        string MapToHotelCode(int hotelID);
        int MapToHotelId(int homeRootNodeId, string hotelCode);
        int MapToHotelId(string hotelCode);
        int MapToLanguageId(EChannel channel);
        int MapToLanguageId(int siteId);
        RatePlanType MapToRatePlanType(string planCode);
        string MapToRoomCode(string hotelCode, string roomCategory);
    }
}