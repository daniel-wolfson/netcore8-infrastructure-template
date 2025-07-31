using AutoMapper;
using Custom.Domain.Optima.Dto;
using Custom.Domain.Optima.Models;
using Custom.Domain.Optima.Models.Availability;
using Custom.Domain.Optima.Models.Enums;
using Custom.Domain.Optima.Models.Main;
using Custom.Domain.Optima.Models.Umbraco;
using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Configuration.Optima;
using Custom.Framework.Configuration.Umbraco;
using Custom.Framework.Contracts;
using Custom.Framework.Exceptions;
using Custom.Framework.Extensions;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Custom.Framework.Models.Base;
using Custom.Framework.StaticData;
using Custom.Framework.StaticData.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace Custom.Framework.Core;

public class ApiOptimaDataMapper<TAppSettings> : Profile, IApiOptimaDataMapper
    where TAppSettings : ApiSettings
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly TAppSettings _appSettings;
    private readonly List<int> _heSiteIds = [1050];

    //private ISearchRequest _currentSearchQuery;
    private List<int> _hotelIDs;
    private int _hotelID;
    private int? _homeRootNodeId;
    private int _channel;
    private int? _originCustomerId;
    private CustomerIds _customerIds;

    //private OptimaContext _apiSettingsContext;
    private OptimaSettings _optimaSettings;
    private IEnumerable<int> _hotelIdsDataSet;
    private IEnumerable<HotelData> _hotelDataSet;
    private IEnumerable<PlanData> _plansSettings;
    private IEnumerable<PriceCodeData> _priceCodeDataSet;
    private IEnumerable<PriceGroupData> _priceGroupDataSet;
    private IEnumerable<PriceCodeCategoryData> _priceCodesCategoriesDataSet;
    private IEnumerable<PriceCodeCategoryLinkData> _priceCodesCategoriesLinkDataSet;
    private IEnumerable<PriceCodeCategoryTranslationsData> _priceCodeCategoryTranslationsDataSet;
    private IEnumerable<PriceCodeTranslationsData> _priceCodeTranslationsDataSet;
    private IEnumerable<PackageTranslationsData> _packageTranslateDataSet;
    private IEnumerable<PackageShowData> _packageShowDataSet;
    private IEnumerable<PolicyData> _policyDataSet;
    private IEnumerable<RoomData> _roomsDataSet;
    private IEnumerable<UmbracoSettings> _umbracoDataSet;

    private readonly IStaticDataService _staticDataService;

    public ApiOptimaDataMapper(IHttpContextAccessor httpContextAccessor,
        IOptions<TAppSettings> appSettings,
        ILogger logger, IConfiguration configuration,
        IStaticDataService staticDataService)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _configuration = configuration;
        _appSettings = appSettings.Value;
        _staticDataService = staticDataService;
        InitAutoMapper();
    }

    #region props

    public int HotelID => _hotelID;
    public List<int> HotelIDs => _hotelIDs;
    public int LanguageID { get; set; }
    public int HomeRootNodeId => _homeRootNodeId ?? 1050;
    public Dictionary<string, List<string>> ErrorList { get; set; } = [];

    public StaticDataCollection<EntityData> StaticDataSource => _staticDataService.DataContext!;

    /// <summary> CurrentSearchQuery is clone from SearchRequest </summary>
    //public ISearchRequest CurrentSearchQuery
    //{
    //    get { return _currentSearchQuery; }
    //    set
    //    {
    //        _currentSearchQuery = value;
    //        _hotelIDs = _currentSearchQuery.HotelCodes.Select(MapToHotelId).Where(x => x > 0).ToList();
    //        _hotelID = _hotelIDs.FirstOrDefault();
    //        _languageId = MapToLanguageId(_currentSearchQuery.Channel);
    //        _originCustomerId = MapToCustomerId(_currentSearchQuery);
    //        value.CustomerId = _originCustomerId;
    //    }
    //}

    #endregion props

    #region settings props

    public void Set(int homeRootNodeId, List<int> hotelIDs, int channel, int customerId)
    {
        _homeRootNodeId = homeRootNodeId;
        _originCustomerId = customerId;
        _hotelIDs = hotelIDs;
        _hotelID = _hotelIDs.FirstOrDefault();
        _channel = channel;
    }

    public CustomerIds CustomerIdDataSet =>
        StaticDataSource?.GetValueOrDefault<OptimaSettings>(SettingKeys.OptimaSettings)?
            .SitesSettings?.FirstOrDefault(x => x.HomeRootNodeId == HomeRootNodeId)?.OptimaCustomerId
        ?? throw new ApiException($"{ApiHelper.LogTitle()}: No optimaSettings.SitesSettings.CustomerIdDataSet for HomeRootNodeId: {HomeRootNodeId}!");

    public UmbracoSettings UmbracoSearchDataSet =>
        UmbracoDataSet.FirstOrDefault(x => x.Id == HomeRootNodeId)
        ?? throw new ApiException($"{ApiHelper.LogTitle()}: {nameof(UmbracoSearchDataSet)} failed for HomeRootNodeId: {HomeRootNodeId}!");

    public OptimaSettings OptimaDataSet =>
        _optimaSettings ??= StaticDataSource.GetValueOrDefault<OptimaSettings>(SettingKeys.OptimaSettings)
        ?? throw new ApiException($"{ApiHelper.LogTitle()}: {nameof(OptimaDataSet)} failed!");

    public IEnumerable<PlanData> PlanDataSet =>
        _plansSettings ??= StaticDataSource.GetValueOrDefault<List<PlanData>>(SettingKeys.Plans)
        ?? throw new ApiException($"{ApiHelper.LogTitle()}: {nameof(PlanDataSet)} failed!");

    public IEnumerable<PriceCodeData> PriceCodeDataSet =>
        _priceCodeDataSet = StaticDataSource.GetValueOrDefault<List<PriceCodeData>>(SettingKeys.PriceCodes)
        ?? throw new ApiException($"{ApiHelper.LogTitle()}: {nameof(PriceCodeDataSet)} failed!");

    public IEnumerable<PriceGroupData> PriceGroupDataSet =>
        _priceGroupDataSet = StaticDataSource.GetValueOrDefault<List<PriceGroupData>>(SettingKeys.PriceGroups)
        ?? throw new ApiException($"{ApiHelper.LogTitle()}: {nameof(PriceGroupDataSet)} failed!");

    public IEnumerable<PriceCodeCategoryData> PriceCodesCategoriesDataset =>
        _priceCodesCategoriesDataSet ??= StaticDataSource.GetValueOrDefault<List<PriceCodeCategoryData>>(SettingKeys.PriceCodeCategories)
        ?? typeof(IEnumerable<PriceCodeCategoryData>).GetDefault<IEnumerable<PriceCodeCategoryData>>();

    public IEnumerable<PriceCodeCategoryLinkData> PriceCodeCategoryLinkDataSet =>
        _priceCodesCategoriesLinkDataSet ??= StaticDataSource.GetValueOrDefault<List<PriceCodeCategoryLinkData>>(SettingKeys.PriceCodeCategoryLinks)
        ?? typeof(IEnumerable<PriceCodeCategoryLinkData>).GetDefault<IEnumerable<PriceCodeCategoryLinkData>>();

    public IEnumerable<PriceCodeCategoryTranslationsData> PriceCodeCategoriesTranslationsDataSet =>
        _priceCodeCategoryTranslationsDataSet ??= StaticDataSource.GetValueOrDefault<List<PriceCodeCategoryTranslationsData>>(SettingKeys.PriceCodeCategoriesTranslations)
        ?? typeof(IEnumerable<PriceCodeCategoryTranslationsData>).GetDefault<IEnumerable<PriceCodeCategoryTranslationsData>>();

    public IEnumerable<PriceCodeTranslationsData> PriceCodeTranslationsDataSet =>
        _priceCodeTranslationsDataSet ??= StaticDataSource.GetValueOrDefault<List<PriceCodeTranslationsData>>(SettingKeys.PriceCodeTranslations)
        ?? typeof(IEnumerable<PriceCodeTranslationsData>).GetDefault<IEnumerable<PriceCodeTranslationsData>>();

    public IEnumerable<RoomData> RoomDataSet =>
        _roomsDataSet ??= OptimaDataSet.CodesConversion
            .Where(x => x.HotelCode.G4Code == this.HotelID.ToString())
            .SelectMany(x => x.Rooms)
            .Select(x => new RoomData() { Code = x.GeneralCode, CodeSource = x.G4Code })
            .ToList();

    public IEnumerable<PackageTranslationsData> PackageTranslationDataSet =>
        _packageTranslateDataSet ??= StaticDataSource.GetValueOrDefault<List<PackageTranslationsData>>(SettingKeys.PackageTranslations) ?? [];

    public IEnumerable<PackageShowData> PackageShowDataSet =>
        _packageShowDataSet ??= StaticDataSource.GetValueOrDefault<List<PackageShowData>>(SettingKeys.PackageShow) ?? [];

    public IEnumerable<PolicyData> PolicyDataSet =>
        _policyDataSet ??= StaticDataSource.GetValueOrDefault<List<PolicyData>>(SettingKeys.Policy) ?? [];

    public IEnumerable<UmbracoSettings> UmbracoDataSet =>
        _umbracoDataSet ??= StaticDataSource.GetValueOrDefault<List<UmbracoSettings>>(SettingKeys.UmbracoSettings) ?? [];

    public IEnumerable<HotelData> HotelDataSet
    {
        get
        {
            _hotelDataSet ??= StaticDataSource.GetValueOrDefault<List<HotelData>>(SettingKeys.Hotels)?
                .Where(x => HotelIdsDataSet.Contains(x.HotelID))
                .ToList() ?? [];
            return _hotelDataSet;
        }
    }

    public IEnumerable<int> HotelIdsDataSet
    {
        get
        {
            _hotelIdsDataSet ??= StaticDataSource.GetValueOrDefault<List<int>>(SettingKeys.HotelIds) ?? [];
            return _hotelIdsDataSet;
        }
    }

    //public OptimaContext ApiSettingsContext => _apiSettingsContext ??= GetService<OptimaContext>();

    #endregion settings props

    #region public MapTo methods

    public Customer GetCustomerIdInfo(int customerId)
    {
        return new Customer(CustomerIdDataSet, customerId);
    }


    public int MapToCustomerId(int rootNodeId, bool isMobile, bool isSunClub, bool isBasePrice = false)
    {
        if (rootNodeId > 0)
        {
            _customerIds = OptimaDataSet.SitesSettings
                .FirstOrDefault(x => x.HomeRootNodeId == rootNodeId)?.OptimaCustomerId
                    ?? throw new ApiException(ServiceStatus.FatalError,
                    $"{ApiHelper.LogTitle("ErrorInfo")} OptimaCustomerId is null, it can't mapped to optima customerID. RootNodeId: {rootNodeId}.");
        }

        return (isBasePrice, isSunClub, isMobile) switch
        {
            (true, _, _) => CustomerIdDataSet.BasePrice,
            (_, true, true) => CustomerIdDataSet.MobileSunClub,
            (_, true, false) => CustomerIdDataSet.DesktopSunClub,
            (false, false, true) => CustomerIdDataSet.MobileGuest,
            _ => CustomerIdDataSet.DesktopGuest
        };
    }

    /// <summary> Map to the CA hotel code </summary>
    public string MapToHotelCode(int hotelID)
    {
        var optimaHotelSettings = OptimaDataSet.CodesConversion
                .FirstOrDefault(x => x.HotelCode.G4Code == hotelID.ToString());

        if (optimaHotelSettings == null)
        {
            _logger.Error("{TITLE} error: unable map a hotelId={HOTELID} to HotelCode",
                ApiHelper.LogTitle(), hotelID);
            return string.Empty;
        }

        var hotelCode = optimaHotelSettings?.HotelCode.GeneralCode ?? "";

        var isValid = !string.IsNullOrEmpty(hotelCode);
        if (!isValid)
            throw new ApiException(ServiceStatus.FatalError,
                $"{ApiHelper.LogTitle()} exception: unable map to HotelCode");

        return hotelCode ?? $"{hotelID}";
    }

    /// <summary> Map to optima hotel id </summary>
    public int MapToHotelId(string hotelCode)
    {
        var isParsed = int.TryParse(OptimaDataSet?
            .CodesConversion?
            .FirstOrDefault(x => x.HotelCode.GeneralCode == hotelCode)?
            .HotelCode?
            .G4Code, out int optimaHotelId);

        if (!isParsed)
            _logger.Error("{TITLE} error: MapToHotelId {HOTELCODE} failed",
                ApiHelper.LogTitle(), hotelCode);

        return optimaHotelId;
    }

    /// <summary> Map to hotelId </summary>
    public int MapToHotelId(int homeRootNodeId, string hotelCode)
    {
        var optimaHotelSettings = OptimaDataSet.CodesConversion
            .FirstOrDefault(x => x.HotelCode.GeneralCode == hotelCode);

        var isValid = int.TryParse(optimaHotelSettings?.HotelCode.G4Code, out int optimaHotelId);

        if (!isValid)
            _logger.Error("{TITLE} error: TryParse to optimaHotelId faled", ApiHelper.LogTitle());

        return optimaHotelId; // TODO: WL add HotelId  110 to OptimaUmbracoSettings;
    }

    public int? MapToGuestId(string? guestId)
    {
        if (guestId == null)
            return null;

        bool isGuestIdValid = int.TryParse(guestId, out int guestID);
        if (!isGuestIdValid)
            _logger.Error("{TITLE} error: seatchQuery.GuestId {GUESTID}");

        return guestID;
    }

    /// <summary> Map to languageId of optima </summary>
    public int MapToLanguageId(EChannel channel)
    {
        switch (channel)
        {
            case EChannel.WHENIS:
            case EChannel.TANIS:
                return 2; // heb
            case EChannel.NONE:
            case EChannel.WCHNIS:
            case EChannel.WENUSD:
            case EChannel.PSEUDO:
            case EChannel.TAUSD:
            default:
                return 1; // eng
        };
    }

    /// <summary> Map to languageId of optima </summary>
    public int MapToLanguageId(int siteId)
    {
        return _heSiteIds.Contains(siteId) ? 2 : 1;
    }

    /// <summary> Map to ratePlanType </summary>
    public RatePlanType MapToRatePlanType(string planCode)
    {
        switch (planCode)
        {
            case "H/B":
                return RatePlanType.STD;
            case "B/B":
                return RatePlanType.STD;
            case "Spa BB":
                return RatePlanType.STD;
            default:
                _logger.Warning("{Code} error: {RatePlanType} not exist in seettings and can't mapped. By default used {DefaultValue}",
                    ApiHelper.LogTitle(), planCode, RatePlanType.STD);
                return RatePlanType.NEG;
        }
    }

    /// <summary> Map to roomCode </summary>
    public string MapToRoomCode(string hotelCode, string roomCategory)
    {
        var optimaHotelSettings = OptimaDataSet.CodesConversion
            .FirstOrDefault(x => x.HotelCode.GeneralCode == hotelCode);

        var roomOptimaSettings = optimaHotelSettings?.Rooms?.FirstOrDefault(x => x.G4Code == roomCategory);
        var isDebugMode = _httpContextAccessor.HttpContext?.IsRequestDebugMode() ?? false;
        if (roomOptimaSettings == null && isDebugMode)
            _logger.Warning("{TITLE} error: {ROOMCODE} cann't mapped to RoomCode and will skiped", ApiHelper.LogTitle(), roomCategory);

        return roomOptimaSettings?.GeneralCode ?? "";
    }

    /// <summary> Map to channel </summary>
    public EChannel MapToChannel(int channel, EChannel? channelDefault = EChannel.WHENIS)
    {
        switch (channel)
        {
            case 1:
                return EChannel.WHENIS;
            case 4:
                return EChannel.TANIS;
            case 5:
                return EChannel.TAUSD;
            case 6:
                return EChannel.PSEUDO;
            default:
                _logger.Warning("{TITLE} error: Channel: {CHANNEL} can't mapped. By default used {DEFAULTVALUE}",
                    ApiHelper.LogTitle(), channel, channelDefault);
                return channelDefault ?? EChannel.WHENIS;
        }
        ;
    }

    public string MapToBoardbase(string plan)
    {
        string pc = plan.Substring(0, 2);
        switch (pc)
        {
            case "RO" or "HB" or "BB" or "AI":
                return pc;
            default:
                Log.Logger.Warning("{TITLE} warning: planName '{PLANNAME}' not exists in settings and can't mapped.",
                    ApiHelper.LogTitle(), plan);
                return string.Empty;
        }
    }

    public string MapToBoardbase(PlanData plan)
    {
        string pc = plan.Name.Substring(0, 2);
        switch (pc)
        {
            case "RO" or "HB" or "BB" or "AI":
                return pc;
            default:
                Log.Logger.Warning("{TITLE} warning: planName '{PLANNAME}' not exists in settings and can't mapped.",
                    ApiHelper.LogTitle(), plan.Name);
                return string.Empty;
        }
    }

    /// <summary> Map to region name </summary>
    public string GetRegionName(string hotelCode, int customerId)
    {
        var hotelId = MapToHotelId(hotelCode);
        var hotel = HotelDataSet.FirstOrDefault(x => x.HotelID == hotelId && x.CustomerId == customerId);

        if (hotel?.CityCode == null)
            _logger.Warning("{TITLE} error: hotel.CityCode: {CITYCODE} not defined", ApiHelper.LogTitle(), hotel?.CityCode);

        return hotel?.CityCode ?? string.Empty;
    }

    #endregion public MapTo methods

    #region public Get methods

    /// <summary> Get room code </summary>
    public string GetRoomCode(int hotelID, string roomCategory)
    {
        var optimaHotelSettings = OptimaDataSet.CodesConversion
            .FirstOrDefault(x => x.HotelCode.G4Code == hotelID.ToString());

        var roomSettings = optimaHotelSettings?.Rooms?.FirstOrDefault(x => x.G4Code == roomCategory);
        var result = roomSettings?.GeneralCode;

        if (string.IsNullOrEmpty(result))
            _logger.Error("{TITLE} error: {ROOMCATEGORY} hasn't been mapped to RoomCode", ApiHelper.LogTitle(), roomCategory);

        return result ?? roomCategory;
    }

    /// <summary> Get currency code </summary>
    public string GetCurrencyCode(string? currencyCode)
    {
        return currencyCode == "NIS" ? "ILS" : currencyCode ?? "ILS";
    }

    /// <summary> Get Currency </summary>
    //public ECurrencyCode GetCurrency(string currencyCode)
    //{
    //    return currencyCode switch
    //    {
    //        "ILS" or "NIS" => ECurrencyCode.ILS,
    //        "USD" => ECurrencyCode.USD,
    //        "EUR" => ECurrencyCode.EUR,
    //        "GBP" => ECurrencyCode.GBP,
    //        _ => ECurrencyCode.ILS,
    //    };
    //}

    /// <summary> Get occupancyCode </summary>
    public int GetOccupancyCode(PackagesList package)
    {
        return package.Adults * 100 + package.Children * 10 + package.Infants;
    }

    /// <summary> Get occupancy </summary>
    public Occupancy GetOccupancy(PackagesList package)
    {
        return new Occupancy
        {
            Adults = package.Adults,
            Children = package.Children,
            Infants = package.Infants
        };
    }

    /// <summary> Get rooms </summary>
    public List<RoomResult> GetPackageRooms(List<Occupancy> occupancies,
        string hotelCode, IEnumerable<PackagesList> packages, string packageResultId, List<Sale> sales)
    {
        var originalOccupancies = occupancies.Select(x => x.OccupancyCode).ToList();

        var splitOccupancies = UmbracoSearchDataSet
            .RoomSeparateCombinations?
            .FirstOrDefault(x => x.HotelCode == hotelCode)?
            .Combinations
            .Where(x => originalOccupancies.Contains(x.OriginalOccupancy))
            .SelectMany(x => x.SplitedOccupancy)
            .Distinct()
            .ToList() ?? [];

        var packageResults = packages
            .DistinctBy(x => x.RoomCategory)
            .Select(package =>
            {
                var requestType = splitOccupancies.Contains(package.OccupancyCode)
                    && !originalOccupancies.Contains(package.OccupancyCode)
                        ? EAvailabilityRequestType.SplitRoom
                        : EAvailabilityRequestType.OriginRoom;

                return new RoomResult()
                {
                    RoomCode = GetRoomCode(package.HotelID, package.RoomCategory),
                    AvailableUnits = package.AvailableRooms,
                    OriginalOccupancyCode = GetOccupancyCode(package),
                    Occupancy = GetOccupancy(package),
                    SingleRoomPackageId = packageResultId,
                    MatchingOccupancyCombinations = requestType == EAvailabilityRequestType.OriginRoom
                        ? splitOccupancies.ToList() : [],
                    RequestType = requestType,
                    IsConnectingRoom = package.IsConnectedDoor,
                    SingleRoomPriceDetails = GetSingleRoomPriceDetails(package, sales)
                };
            })
            .ToList();

        return packageResults;
    }

    public async Task<TResult> GetServiceResult<TRequest, TResult>(
            string apiHttpClientName, string path, TRequest request)
            where TRequest : OptimaRequest, new()
            where TResult : IOptimaResult, new()
    {
        TResult result;

        var httpClientFactory = GetService<IApiHttpClientFactory>();
        var optimaRoomsApi = httpClientFactory.CreateClient(apiHttpClientName);
        request.UserName = _appSettings.Optima.UserName;
        request.Password = _appSettings.Optima.Password;

        var postResult = await optimaRoomsApi.GetAsync<TRequest, TResult>(path, request);

        if (postResult == null)
            postResult = ServiceResult<TResult>.Error($"{ApiHelper.LogTitle()} failed. Code: {postResult?.Message}");

        result = postResult.Value != null ? postResult.Value : new TResult();
        result.RequestUrl = postResult.RequestUrl ?? "";
        result.RequestData = request;

        return result;
    }

    #endregion public Get methods

    #region private methods
    private List<SingleRoomPriceDetails> GetSingleRoomPriceDetails(PackagesList package, List<Sale> sales)
    {
        var details = new List<SingleRoomPriceDetails>();
        try
        {
            string roomCode = GetRoomCode(package.HotelID, package.RoomCategory);
            var occupancy = GetOccupancy(package);
            foreach (var sale in sales)
            {
                foreach (var boardBase in sale.BoardBases)
                {
                    details.Add(new SingleRoomPriceDetails
                    {
                        UniqueId = boardBase.SessionID,
                        BoardBaseCode = boardBase.BoardBaseCode,
                        OccupancyCode = occupancy.ToString(),
                        RoomCode = roomCode,
                        SaleId = sale.SaleNopId,
                        SaleSource = sale.SaleSource,
                        GuestPrice = boardBase.GuestRoomPrice,
                        SunClubPrice = boardBase.SunClubRoomPrice
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

        }
        return details;
    }

    /// <summary> GetService go to get the instance of TFilterType from registered services </summary>
    protected T GetService<T>(string? serviceKey = null)
    {
        if (_httpContextAccessor.HttpContext == null)
            throw new ApiException(ServiceStatus.FatalError, "HttpContext not defined");

        if (!string.IsNullOrEmpty(serviceKey))
            return _httpContextAccessor.HttpContext.RequestServices.GetKeyedService<T>(serviceKey)!;

        var service = _httpContextAccessor.HttpContext.RequestServices.GetService(typeof(T))
            ?? throw new ApiException(ServiceStatus.FatalError, $"{nameof(GetService)} error: {typeof(T).Name} not registered");

        return (T)service;
    }

    protected T GetOptions<T>(SettingKeys? serviceKey)
        where T : class
    {
        // Check if HttpContext is available
        if (_httpContextAccessor.HttpContext == null)
        {
            throw new ApiException(ServiceStatus.FatalError, "HttpContext not defined");
        }

        // If a serviceKey is provided, retrieve the keyed service
        // Attempt to get the IOptionsSnapshot<T> service by key
        var options = _httpContextAccessor.HttpContext.RequestServices.GetKeyedService<IOptionsSnapshot<T>>(serviceKey)
            ?? throw new ApiException(ServiceStatus.FatalError, $"Options with key {serviceKey} not found");

        // Return the options value
        if (options != null)
            return options.Value;

        // If no serviceKey is provided, retrieve the service directly
        var service = _httpContextAccessor.HttpContext.RequestServices.GetService(typeof(IOptionsSnapshot<T>))
            ?? throw new ApiException(ServiceStatus.FatalError, $"Options with key {serviceKey} not found");

        // Cast and return the service
        return (T)service;
    }

    #endregion private methods

    #region private methods

    private void InitAutoMapper()
    {
        // Mapping configuration
        //CreateMap<ISearchRequest, int>().ConvertUsing(src => MapToCustomerId(src));
        CreateMap<(bool isMobile, bool isSunclub, bool IsBasePrice), int>()
            .ConvertUsing(src => MapToCustomerId(0, src.isMobile, src.isSunclub, src.IsBasePrice));
        CreateMap<int, string?>().ConvertUsing(src => MapToHotelCode(src));
        CreateMap<string, int>().ConvertUsing(src => MapToHotelId(src));
        CreateMap<(int homeRootNodeId, string hotelCode), int>()
            .ConvertUsing(src => MapToHotelId(src.homeRootNodeId, src.hotelCode));
        CreateMap<string?, int?>().ConvertUsing(src => MapToGuestId(src));
        CreateMap<EChannel, int>().ConvertUsing(src => MapToLanguageId(src));
        CreateMap<string, RatePlanType>().ConvertUsing(src => MapToRatePlanType(src));
        CreateMap<(string hotelCode, string roomCategory), string?>()
            .ConvertUsing(src => MapToRoomCode(src.hotelCode, src.roomCategory));
        CreateMap<int, EChannel>().ConvertUsing(src => MapToChannel(src, EChannel.WHENIS));
        CreateMap<(int channel, EChannel channelDefault), EChannel>()
            .ConvertUsing(src => MapToChannel(src.channel, src.channelDefault));
        CreateMap<PlanData, PlanData>().ForMember(dest => dest.BoardBaseCode, opt => opt.MapFrom(src => src.ToBoardBase()));
    }

    #endregion private methods
}
