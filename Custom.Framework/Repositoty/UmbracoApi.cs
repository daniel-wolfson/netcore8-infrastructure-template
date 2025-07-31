using Custom.Domain.Optima.Models.Umbraco;
using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Configuration.Umbraco;
using Custom.Framework.Contracts;
using Custom.Framework.Core;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Custom.Framework.Repositoty
{
    public class UmbracoApi(
        IHttpContextAccessor httpContextAccessor)
        : ApiBase(httpContextAccessor), IUmbracoApi
    {
        #region public methods

        /// <summary> GetAsync2 - GetFromRedis or (ifNull)DownloadFromBlob or (ifNull)GetFromUmbaco </summary>
        public async Task<IServiceResult<TData>> GetAsync<TData>(SettingKeys settingKey, CancellationToken cancelToken = default)
        {
            try
            {
                TData? objectData = default;

                var exists = await RedisCache.HashExistsAsync(RootCacheKey, settingKey.ToString());
                if (exists)
                {
                    var result = await RedisCache.HashGetAsync<List<RoomData>>(RootCacheKey, settingKey.ToString());
                    //if (IsDataNotNullOrEmpty<TData>(result))
                    objectData = (TData?)(object?)result;
                }

                if (objectData == null)
                {
                    if (typeof(TData) == typeof(object))
                    {
                        var downloadResult = await BlobStorage.DownloadAsync(AppSettings.Version, settingKey.ToString(), settingKey.GetResourceType());
                        objectData = (TData?)downloadResult;
                    }
                    else
                    {
                        var data = await BlobStorage.DownloadAsync<TData>(AppSettings.Version, settingKey.ToString());
                        objectData ??= data.Value;
                    }
                }

                if (objectData == null)
                {
                    var results = await ReadAsync<TData>(settingKey, cancelToken);
                    //var optimaResult = await OptimaBaseRepository.ReadAsync<TData>(settingKey, cancelToken);
                    objectData = results.Value;
                }

                if (objectData != null)
                    return ServiceResult<TData>.Ok(objectData ?? settingKey.GetDefault<TData>());
                else
                    return ServiceResult<TData>.Default(settingKey.GetDefault<TData>());
            }
            catch (Exception ex)
            {
                var errMsg = ex.InnerException?.Message ?? ex.Message;
                Logger.Error("{TITLE} error: get key {KEY} failed. Exception: {ERROR}. \nStackTrace: {STACKTRACE}\n",
                       ApiHelper.LogTitle(), settingKey, errMsg, ex.StackTrace);

                return ServiceResult<TData>.Error(errMsg, settingKey.GetDefault<TData>());
            }
        }

        public async Task<IServiceResult<List<UmbracoSettings>>> GetUmbracoSearchSettings<TData>(CancellationToken cancellationToken = default)
        {
            try
            {
                var homeRootNodeId = 1050; // TODO: Get dat for all root nod id
                IServiceScope serviceScope = ServiceScopeFactory.CreateScope();
                var httpClientFactory = serviceScope.ServiceProvider.GetService<IApiHttpClientFactory>()!;

                using var httpClient = httpClientFactory.CreateClient(ApiHttpClientNames.UmbracoApi);
                var path = $"{AppSettings.Umbraco.RoomsPath}?rootNodeId={homeRootNodeId}";
                var result = await httpClient.GetAsync<List<UmbracoSettings>>(path);
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            return ServiceResult<List<UmbracoSettings>>.Default(default);
        }

        /// <summary> DEV, Not for use </summary>
        public async Task<IServiceResult<TData>> ReadAsync<TData>(SettingKeys settingKey, CancellationToken cancellationToken = default)
        {
            try
            {
                switch (settingKey)
                {
                    case SettingKeys.Rooms:
                        //var roomResults = await GetRoomData<List<RoomData>>(cancellationToken);
                        //if (roomResults.Value != null)
                        //    return ServiceResult<TData>.Ok((TData)(object)roomResults.Value);
                        //else
                        return ServiceResult<TData>.Default(settingKey.GetDefault<TData>());

                        //case SettingKeys.UmbracoSettings:
                        //    var umbResults = await GetUmbSettings<List<UmbracoSettings>>(cancellationToken);
                        //    if (umbResults.Code != null)
                        //        return ServiceResult<TData>.Ok((TData)(object)umbResults.Code);
                        //    else
                        //        return ServiceResult<TData>.ErrorDefault(settingKey.GetDefault<TData>());
                }


            }
            catch (Exception ex)
            {
                Logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            return ServiceResult<TData>.Default(default);
        }

        public async Task<IServiceResult<bool>> UploadToBlobAsync(SettingKeys settingKey, CancellationToken cancelToken = default)
        {
            try
            {
                var results = await ReadAsync<object>(settingKey, cancelToken);

                if (!ApiHelper.IsDataNullOrEmpty(results.Value))
                {
                    var uploadResult = await BlobStorage.UploadAsync(AppSettings.Version, settingKey.ToString(), results.Value);
                    if (uploadResult)
                    {
                        var setResult = await RedisCache.HashSetAsync(RootCacheKey, settingKey.ToString(), results.Value);
                        return ServiceResult<bool>.Ok(setResult);
                    }
                }

                return ServiceResult<bool>.Error("Data result from umbraco isnull");
            }
            catch (Exception ex)
            {
                var errMsg = ex.InnerException?.Message ?? ex.Message;
                Logger.Error("{TITLE} error: get key {KEY} failed. Exception: {ERROR}. \nStackTrace: {STACKTRACE}\n",
                       ApiHelper.LogTitle(), settingKey, errMsg, ex.StackTrace);

                return ServiceResult<bool>.Error(errMsg);
            }
        }

        #endregion public methods

        #region private methods

        /// <summary> Get Room Data </summary>
        private async Task<IServiceResult<List<RoomData>>> GetRoomData<TData>(CancellationToken cancellationToken = default)
        {
            try
            {
                var homeRootNodeId = 1050; // TODO: Get dat for all root nod id
                IServiceScope serviceScope = ServiceScopeFactory.CreateScope();
                var httpClientFactory = serviceScope.ServiceProvider.GetService<IApiHttpClientFactory>()!;

                using var httpClient = httpClientFactory.CreateClient(ApiHttpClientNames.UmbracoApi);
                var path = $"{AppSettings.Umbraco.RoomsPath}?rootNodeId={homeRootNodeId}";
                var result = await httpClient.GetAsync<List<RoomData>>(path);
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            return ServiceResult<List<RoomData>>.Default();
        }

        // TODO: Implement
        private async Task<IServiceResult<List<UmbracoSettings>>> GetUmbSettings<TData>(CancellationToken cancellationToken = default)
        {
            try
            {
                var homeRootNodeId = 1050; // TODO: Get dat for all root nod id
                IServiceScope serviceScope = ServiceScopeFactory.CreateScope();
                var httpClientFactory = serviceScope.ServiceProvider.GetService<IApiHttpClientFactory>()!;

                using var httpClient = httpClientFactory.CreateClient(ApiHttpClientNames.UmbracoApi);
                var path = $"{AppSettings.Umbraco.RoomsPath}?rootNodeId={homeRootNodeId}";
                var result = await httpClient.GetAsync<List<UmbracoSettings>>(path);
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            return ServiceResult<List<UmbracoSettings>>.Default();
        }

        #endregion private methods
    }
}