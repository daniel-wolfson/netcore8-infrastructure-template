using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Serilog;
using System.Runtime.CompilerServices;

namespace Custom.Framework.Cache
{
    public class ApiMemoryCacheOptions : IApiCacheOptions
    {
        private readonly ApiSettings _appSettings;
        private readonly ILogger _logger;

        public ApiMemoryCacheOptions(IOptions<ApiSettings> appSettingsOptions, ILogger logger)
        {
            _appSettings = appSettingsOptions.Value;
            _logger = logger;
        }

        /// <summary>
        /// GetMemoryCacheOptions for SettingKeys, OnPostEviction - callback which gets called when a cache entry expires.
        /// </summary>
        public MemoryCacheEntryOptions GetMemoryCacheOptions(SettingKeys settingsKey,
            int reloadInterval,
            PostEvictionDelegate? OnPostEviction,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            TimeSpan timeSpan;

            switch (settingsKey)
            {
                case SettingKeys.UmbracoSettings:
                case SettingKeys.RoomSpecialRequestsCodes:
                case SettingKeys.NopSearchSettings:
                case SettingKeys.AllRatePlanCodes:
                case SettingKeys.DynamicFlightSettings:
                case SettingKeys.Sales:
                case SettingKeys.SalesPromotion:
                case SettingKeys.ReservationSettings:
                case SettingKeys.SunClubSettings:
                case SettingKeys.CancelReservationBccAddresses:
                case SettingKeys.OfflinePricesForAllRooms:
                case SettingKeys.ReservationTerms:
                case SettingKeys.ConnectingDoorReservationEmailAddresses:
                case SettingKeys.ApiSettings:
                case SettingKeys.OptimaSettings:
                    timeSpan = TimeSpan.FromSeconds(60);
                    break;
                default:
                    var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);
                    var title = $"{callerTypeName}.{callerMemberName}";
                    _logger.Error("{TITLE} error: {KEY} parsing not implemented", title, settingsKey);
                    timeSpan = TimeSpan.FromSeconds(60);
                    break;
            }

            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(timeSpan); //.SetSize(3600)
            if (OnPostEviction != null)
                cacheEntryOptions.RegisterPostEvictionCallback(OnPostEviction);
            return cacheEntryOptions;
        }

        public MemoryCacheEntryOptions GetMemoryCacheOptions(string settingsKey,
            int reloadInterval,
            PostEvictionDelegate? OnPostEviction,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(60);

            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(timeSpan);

            if (OnPostEviction != null)
                cacheEntryOptions.RegisterPostEvictionCallback(OnPostEviction);
            return cacheEntryOptions;
        }

        /// <summary>
        /// GetMemoryCacheOptions for CacheKeys, OnPostEviction - callback which gets called when a cache entry expires.
        /// </summary>
        public MemoryCacheEntryOptions GetMemoryCacheOptions(CacheKeys settingsKey,
            int reloadInterval,
            PostEvictionDelegate? OnPostEviction,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            TimeSpan timeSpan;

            switch (settingsKey)
            {
                case CacheKeys.UmbracoSettings:
                case CacheKeys.RoomSpecialRequestsCodes:
                case CacheKeys.NopSearchSettings:
                case CacheKeys.AllRatePlanCodes:
                case CacheKeys.DynamicFlightSettings:
                case CacheKeys.Sales:
                case CacheKeys.SalesPromotion:
                case CacheKeys.ReservationSettings:
                case CacheKeys.SunClubSettings:
                case CacheKeys.CancelReservationBccAddresses:
                case CacheKeys.OfflinePricesForAllRooms:
                case CacheKeys.ReservationTerms:
                case CacheKeys.ConnectingDoorReservationEmailAddresses:
                case CacheKeys.ApiSettings:
                case CacheKeys.OptimaSettings:
                    timeSpan = TimeSpan.FromSeconds(60);
                    break;
                default:
                    var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);
                    var title = $"{callerTypeName}.{callerMemberName}";
                    _logger.Error("{TITLE} error: {KEY} parsing not implemented", title, settingsKey);
                    timeSpan = TimeSpan.FromSeconds(60);
                    break;
            }

            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(timeSpan);

            if (OnPostEviction != null)
                cacheEntryOptions.RegisterPostEvictionCallback(OnPostEviction);
            return cacheEntryOptions;
        }

        public int GetMemoryCacheTtl(CacheKeys settingsKey,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            TimeSpan timeSpan;
            var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);
            var title = $"{callerTypeName}.{callerMemberName}";

            switch (settingsKey)
            {
                case CacheKeys.UmbracoSettings:
                case CacheKeys.RoomSpecialRequestsCodes:
                case CacheKeys.NopSearchSettings:
                case CacheKeys.AllRatePlanCodes:
                case CacheKeys.DynamicFlightSettings:
                case CacheKeys.Sales:
                case CacheKeys.SalesPromotion:
                case CacheKeys.ReservationSettings:
                case CacheKeys.SunClubSettings:
                case CacheKeys.CancelReservationBccAddresses:
                case CacheKeys.OfflinePricesForAllRooms:
                case CacheKeys.ReservationTerms:
                case CacheKeys.ConnectingDoorReservationEmailAddresses:
                case CacheKeys.ApiSettings:
                case CacheKeys.OptimaSettings:
                    timeSpan = TimeSpan.FromSeconds(30); // TODO: Need to implement for each from SettingKeys
                    break;
                default:
                    _logger.Error("{TITLE} error: {KEY} parsing not implemented", title, settingsKey);
                    timeSpan = TimeSpan.FromSeconds(30);
                    break;
            }

            return (int)timeSpan.TotalSeconds;
        }

        public int GetRedisCacheTtl(CacheKeys settingsKey,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            TimeSpan timeSpan;
            var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);
            var title = $"{callerTypeName}.{callerMemberName}";

            switch (settingsKey)
            {
                case CacheKeys.UmbracoSettings:
                case CacheKeys.RoomSpecialRequestsCodes: // TODO: Need to imlement all SettingKeys
                case CacheKeys.NopSearchSettings:
                case CacheKeys.AllRatePlanCodes:
                case CacheKeys.DynamicFlightSettings:
                case CacheKeys.Sales:
                case CacheKeys.SalesPromotion:
                case CacheKeys.ReservationSettings:
                case CacheKeys.SunClubSettings:
                case CacheKeys.CancelReservationBccAddresses:
                case CacheKeys.OfflinePricesForAllRooms:
                case CacheKeys.ReservationTerms:
                case CacheKeys.ConnectingDoorReservationEmailAddresses:
                case CacheKeys.ApiSettings:
                case CacheKeys.OptimaSettings:
                    timeSpan = TimeSpan.FromMinutes(60);
                    break;
                default:
                    _logger.Error("{TITLE} error: {KEY} parsing not implemented", title, settingsKey);
                    timeSpan = TimeSpan.FromMinutes(60);
                    break;
            }

            return (int)timeSpan.TotalSeconds;
        }
    }

    //public class ApiSettingsFactory
    //{
    //    public IApiSettingsManager<TFilterType> CreateApiSettingsManager<TFilterType>(string apiName)
    //    {
    //        switch (apiName)
    //        {
    //            case "Api1":
    //                return new UmbracoSettingsManager<TFilterType>();
    //            default:
    //                throw new ArgumentException("Unsupported API");
    //        }
    //    }
    //}
}