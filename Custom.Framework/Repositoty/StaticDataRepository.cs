using Custom.Domain.Optima.Models.Customer;
using Custom.Domain.Optima.Models.Main;
using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Configuration.Optima;
using Custom.Framework.Contracts;
using Custom.Framework.Core;
using Custom.Framework.Exceptions;
using Custom.Framework.Extensions;
using Custom.Framework.Helpers;
using Custom.Framework.Models.Base;
using Custom.Framework.Models.Main;
using Custom.Framework.Validation;
using Microsoft.AspNetCore.Http;

namespace Custom.Framework.Repositoty
{
    public class StaticDataRepository(IHttpContextAccessor httpContextAccessor)
        : OptimaBaseRepository(httpContextAccessor), IStaticDataRepository
    {
        private OptimaRequest _optimaBaseRequest = default!;

        #region props

        public ApiHttpClient OptimaMainApi => HttpClientFactory.CreateClient(ApiHttpClientNames.OptimaMainApi);

        public OptimaRequest OptimaBaseRequest => _optimaBaseRequest ??= new()
        {
            UserName = AppSettings.Optima.UserName ?? "",
            Password = AppSettings.Optima.Password ?? ""
        };

        #endregion props

        #region public methods

        /// <summary> ReadAsync from Optima </summary>
        public async Task<OptimaResult<TData>> GetAsync<TData>(SettingKeys settingKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var optimaResult = await ReadAsync<TData>(settingKey, cancellationToken);
                return optimaResult;
            }
            catch (Exception ex)
            {
                var errMsg = ex.InnerException?.Message ?? ex.Message;
                Logger.Error("{TITLE} error: get key {KEY} failed. Exception: {ERROR}. \nStackTrace: {STACKTRACE}\n",
                       ApiHelper.LogTitle(), settingKey, errMsg, ex.StackTrace);
                var defaultData = settingKey.GetResourceType().GetDefault<TData>();
                return Error(errMsg, defaultData);
            }
        }

        /// <summary> Read from Optima </summary>
        public async Task<OptimaResult<TData>> ReadAsync<TData>(
            SettingKeys settingKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var customerIds = StaticData
                   .FirstOrDefault(x => x.SettingKey == SettingKeys.CustomerIds)?
                   .Value as List<int> ?? [];
                var hotelIds = StaticData
                   .FirstOrDefault(x => x.SettingKey == SettingKeys.HotelIds)?
                   .Value as List<int> ?? [];
                var hotelCodes = StaticData
                   .FirstOrDefault(x => x.SettingKey == SettingKeys.HotelCodes)?
                   .Value as List<string> ?? [];

                var results = settingKey switch
                {
                    //SettingKeys.Hotels => await GetHotelCodes<TData>(),
                    //SettingKeys.Regions => await GetRegions<TData>(),
                    //SettingKeys.RoomTranslations => await GetRoomTranslations<TData>(),

                    SettingKeys.HotelIds => await GetHotelIds<TData>(),
                    SettingKeys.HotelCodes => await GetHotelCodes<TData>(),
                    SettingKeys.CustomerIds => await GetCustomerIds<TData>(),
                    SettingKeys.PackageShow => await GetAvailabilityPackageShow<TData>(hotelIds, customerIds),
                    SettingKeys.Plans => await GetPlans<TData>(customerIds),
                    SettingKeys.Hotels => await GetHotels<TData>(customerIds),
                    SettingKeys.PriceGroups => await GetPriceGroups<TData>(customerIds),
                    SettingKeys.PriceCodes => await GetPriceCodes<TData>(customerIds),
                    SettingKeys.PriceCodeCategories => await GetPriceCodeCategories<TData>(customerIds),
                    SettingKeys.PriceCodeCategoryLinks => await GetPriceCodeCategoryLinks<TData>(customerIds),
                    SettingKeys.PriceCodeTranslations => await GetPriceCodeTranslations<TData>(customerIds),
                    SettingKeys.PriceCodeCategoriesTranslations => await GetPriceCodeCategoryTranslations<TData>(customerIds),
                    SettingKeys.PackageTranslations => await GetPackageTranslations<TData>(customerIds),
                    SettingKeys.Policy => await GetPolicies<TData>(customerIds),
                    SettingKeys.Clerks => await GetClerksAsync<TData>(hotelIds),
                    _ => new OptimaResult<TData>(
                        typeof(TData).GetDefault<TData>(),
                        $"{ApiHelper.LogTitle()} error: settingsKey {settingKey} have not the matching handler", true)
                };

                if (results.Error)
                    Logger.Error("{TITLE} error: get key {KEY} failed. ErrorInfo: {ERROR}",
                       ApiHelper.LogTitle(), settingKey, results.Message);
                else
                {
                    var staticDataItem = StaticData.FirstOrDefault(x => x.SettingKey == settingKey);
                    if (staticDataItem != null)
                        staticDataItem.Value = results.Data;
                }

                results.Data ??= typeof(TData).GetDefault<TData?>();

                return results!;
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                Logger.Error("{TITLE} error: get key {KEY} failed. Exception: {ERROR}. \nStackTrace: {STACKTRACE}\n",
                       ApiHelper.LogTitle(), settingKey, msg, ex.StackTrace);
                return Error<TData>(msg);
            }
        }

        /// <summary> Read from Optima async </summary>
        /// <summary> Read Regions from Optima </summary>
        public async Task<OptimaResult<TData?>> GetRegions<TData>()
        {
            try
            {
                var entities = await GetRegions();
                var entitieData = typeof(TData) == typeof(object)
                    ? (TData)(object)entities.Data!
                    : (TData)Convert.ChangeType(entities, typeof(TData));
                string message = entities.Message?.Text ?? "";
                var error = entities.Error;
                return error ? Error(message, entitieData) : Ok(entitieData, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<TData>(ex);
            }
        }

        /// <summary> Read Regions from Optima </summary>
        public async Task<OptimaResult<List<RegionData>>> GetRegions()
        {
            try
            {
                var path = Config.GetSection(SettingKeys.Regions)?.Path!;
                var regionsResult = await SendAsync<List<RegionData>>(
                    ApiHttpClientNames.OptimaMainApi, HttpMethod.Post, path, OptimaBaseRequest);
                var message = regionsResult.Message?.Text;
                var error = regionsResult.Error;
                return error ? Error(message, regionsResult.Data) : Ok(regionsResult.Data!, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<List<RegionData>>(ex);
            }
        }

        /// <summary> Get PackageShow from Optima </summary>
        public async Task<OptimaResult<TData>> GetAvailabilityPackageShow<TData>(List<int> hotelIds, List<int> customerIds)
        {
            try
            {
                var entities = await GetAvailabilityPackageShow(hotelIds, customerIds);
                var entitieData = typeof(TData) == typeof(object)
                    ? (TData)(object)entities.Data!
                    : (TData)Convert.ChangeType(entities, typeof(TData));
                string message = entities.Message?.Text ?? "";
                var error = entities.Error;
                return error ? Error(message, entitieData) : Ok(entitieData, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<TData>(ex);
            }
        }

        public virtual async Task<OptimaResult<List<PackageShowData>>> GetAvailabilityPackageShow(
            List<int> hotelIds, List<int> customerIds)
        {
            try
            {
                var path = Config.GetSection(SettingKeys.StaticData, SettingKeys.PackageShow)?.Path!;
                var requests = MakeRequests(customerIds, hotelIds, true);

                var results = await SendBulkAsync<PackageShowData>(ApiHttpClientNames.OptimaMainApi,
                    HttpMethod.Post, path, requests);
                var dataResults = results.SelectMany(x => x.Data ?? []).ToList();
                var message = string.Join(", ", results.SelectMany(x => x.Message?.Text ?? ""));
                var error = results.Any(x => x.Error);
                return error ? Error(message, dataResults) : Ok(dataResults);
            }
            catch (Exception ex)
            {
                return ErrorDefault<List<PackageShowData>>(ex);
            }
        }

        /// <summary> Get Plans from Optima </summary>
        public async Task<OptimaResult<TData>> GetPlans<TData>(List<int> customerIds)
        {
            try
            {
                var apiPath = Config.GetSection(SettingKeys.Plans)?.Path!;
                var requests = MakeRequests(customerIds);

                var planResultList = await SendBulkAsync<PlanData>(
                    ApiHttpClientNames.OptimaMainApi, HttpMethod.Get, apiPath, requests);

                var planResults = planResultList.SelectMany(x => x.Data!).ToList() ?? [];
                planResults.ForEach(plan => plan.BoardBaseCode = plan.ToBoardBase());

                var optimaPlanDataValidator = GetService<IOptimaPlanCodeValidator>();
                planResults.RemoveAll(x => !optimaPlanDataValidator.IsPlanCodeValid(x));

                var message = planResultList.FirstOrDefault()?.Message?.Text ?? "";
                var error = planResultList.FirstOrDefault()?.Error;

                var entitieData = typeof(TData) == typeof(object)
                    ? (TData)(object)planResults
                    : (TData)Convert.ChangeType(planResultList, typeof(TData));

                return error.HasValue && error.Value == true
                    ? Error(message, entitieData)
                    : Ok(entitieData, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<TData>(ex);
            }
        }

        /// <summary> Get Plans from Optima </summary>
        public async Task<OptimaResult<List<PlanData>>> GetPlans(List<int> customerIds)
        {
            try
            {

                var section = Config.GetSection(SettingKeys.Plans);
                var plansSettingsRequest = new { OptimaBaseRequest.UserName, OptimaBaseRequest.Password };
                var plansResult = await SendAsync<List<PlanData>>(ApiHttpClientNames.OptimaMainApi,
                    HttpMethod.Get, section.Path, plansSettingsRequest);
                plansResult.Data = plansResult.Data!.DistinctBy(x => x.PlanCode).ToList();
                plansResult.Data.ForEach(plan => plan.BoardBaseCode = plan.ToBoardBase());

                var optimaPlanDataValidator = GetService<IOptimaPlanCodeValidator>();
                plansResult.Data.RemoveAll(x => !optimaPlanDataValidator.IsPlanCodeValid(x));

                var message = plansResult.Message?.Text;
                var error = plansResult.Error;
                return !error ? Ok(plansResult.Data, message) : Error(message, plansResult.Data);
            }
            catch (Exception ex)
            {
                return ErrorDefault<List<PlanData>>(ex);
            }
        }

        /// <summary> Get PriceGroups from Optima </summary>
        public async Task<OptimaResult<TData>> GetPriceGroups<TData>(List<int> customerIds)
        {
            try
            {
                var entities = await GetPriceGroups(customerIds);
                var entitieData = typeof(TData) == typeof(object)
                    ? (TData)(object)entities.Data
                    : (TData)Convert.ChangeType(entities, typeof(TData));
                string message = entities.Message?.Text ?? "";
                var error = entities.Error;
                return error ? Error(message, entitieData) : Ok(entitieData, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<TData>(ex);
            }
        }

        /// <summary> Get PriceGroups from Optima </summary>
        public async Task<OptimaResult<List<PriceGroupData>>> GetPriceGroups(List<int> customerIds)
        {
            try
            {
                var path = Config.GetSection(SettingKeys.PriceGroups)?.Path ?? throw new ApiException("The path cannot be null.");
                var requests = MakeRequests(customerIds);
                var results = await SendBulkAsync<PriceGroupData>(ApiHttpClientNames.OptimaMainApi,
                    HttpMethod.Get, path, requests);
                var dataResults = results.SelectMany(x => x.Data ?? []).ToList();
                var message = string.Join(", ", results?.SelectMany(x => x.Message?.Text ?? "") ?? []);
                var error = results?.Any(x => x.Error) ?? false;
                return error ? Error(message, dataResults) : Ok(dataResults);
            }
            catch (Exception ex)
            {
                return ErrorDefault<List<PriceGroupData>>(ex);
            }
        }

        /// <summary> Get PriceCodes from Optima </summary>
        public async Task<OptimaResult<TData>> GetPriceCodes<TData>(List<int> customerIds)
        {
            try
            {
                var entities = await GetPriceCodes(customerIds);
                var entitieData = typeof(TData) == typeof(object)
                    ? (TData)(object)entities.Data!
                    : (TData)Convert.ChangeType(entities, typeof(TData));
                string message = entities.Message?.Text ?? "";
                var error = entities.Error;
                return error ? Error(message, entitieData) : Ok(entitieData, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<TData>(ex);
            }
        }

        /// <summary> Get PriceCodes from Optima </summary>
        public async Task<OptimaResult<List<PriceCodeData>>> GetPriceCodes(List<int> customerIds)
        {
            try
            {
                var section = Config
                    .GetSections(SettingKeys.StaticData.ToString())
                    .FirstOrDefault(x => x.SettingKey == SettingKeys.PriceCodes);

                var requests = customerIds.Select(x =>
                    new OptimaRequest
                    {
                        CustomerID = x,
                        UserName = OptimaBaseRequest.UserName,
                        Password = OptimaBaseRequest.Password
                    })
                    .ToArray();

                var results = await SendBulkAsync<PriceCodeData>(
                    ApiHttpClientNames.OptimaMainApi, HttpMethod.Get, section.Path, requests);

                var dataResults = results.SelectMany(x => x.Data ?? []).ToList();
                var message = string.Join(", ", results.SelectMany(x => x.Message?.Text ?? ""));
                var error = results.Any(x => x.Error);
                return error ? Error(message, dataResults) : Ok(dataResults);
            }
            catch (Exception ex)
            {
                return ErrorDefault<List<PriceCodeData>>(ex);
            }
        }

        /// <summary> Get PriceCodeCategories from Optima </summary>
        public async Task<OptimaResult<TData>> GetPriceCodeCategories<TData>(List<int> customerIds)
        {
            try
            {
                var entities = await GetPriceCodeCategories(customerIds);
                var entitieData = typeof(TData) == typeof(object)
                    ? (TData)(object)entities.Data!
                    : (TData)Convert.ChangeType(entities, typeof(TData));
                string message = entities.Message?.Text ?? "";
                var error = entities.Error;
                return error ? Error(message, entitieData) : Ok(entitieData, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<TData>(ex);
            }
        }

        /// <summary> Get PriceCodeCategories from Optima </summary>
        public async Task<OptimaResult<List<PriceCodeCategoryData>>> GetPriceCodeCategories(List<int> customerIds)
        {
            try
            {
                var path = Config.GetSection(SettingKeys.PriceCodeCategories)?.Path ?? throw new ApiException("PriceCodeCategories path cannot be null.");
                var requests = customerIds.Select(x =>
                    new OptimaRequest
                    {
                        CustomerID = x,
                        UserName = OptimaBaseRequest.UserName,
                        Password = OptimaBaseRequest.Password
                    })
                    .ToArray();
                var results = await SendBulkAsync<PriceCodeCategoryData>(ApiHttpClientNames.OptimaMainApi,
                    HttpMethod.Get, path, requests);
                var dataResults = results.SelectMany(x => x.Data ?? []).ToList();
                var message = string.Join(", ", results.SelectMany(x => x.Message?.Text ?? ""));
                var error = results.Any(x => x.Error);
                return error ? Error(message, dataResults) : Ok(dataResults);
            }
            catch (Exception ex)
            {
                return ErrorDefault<List<PriceCodeCategoryData>>(ex);
            }
        }

        /// <summary> Get RoomTranslations from Optima </summary>
        public virtual async Task<OptimaResult<List<RoomTranslationsData>>> GetRoomTranslations()
        {
            try
            {
                var path = Config.GetSection(SettingKeys.RoomTranslations)?.Path ?? throw new ApiException("RoomTranslations path cannot be null.");
                var roomResult = await SendAsync<List<RoomTranslationsData>>(ApiHttpClientNames.OptimaMainApi,
                    HttpMethod.Post, path, OptimaBaseRequest);
                var message = roomResult.Message?.Text;
                var error = roomResult.Error;
                return error ? Error(message, roomResult.Data) : Ok(roomResult.Data, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<List<RoomTranslationsData>>(ex.InnerException?.Message ?? ex.Message);
            }
        }

        public async Task<OptimaResult<TData>> GetCustomerIds<TData>()
        {
            try
            {
                var hotelIds = StaticData
                   .FirstOrDefault(x => x.SettingKey == SettingKeys.CustomerIds)?
                   .Value as List<int> ?? [];

                var entitieData = typeof(TData) == typeof(object)
                    ? (TData)(object)hotelIds!
                    : (TData)Convert.ChangeType(hotelIds, typeof(TData));

                string message = hotelIds.Count > 0 ? "" : "CustomerIds are empty";
                var error = hotelIds.Count == 0;
                var result = error
                    ? Error(message, entitieData)
                    : Ok(entitieData, message);

                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return ErrorDefault<TData>(ex);
            }
        }

        /// <summary> Get Hotels from Optima </summary>
        public async Task<OptimaResult<TData>> GetHotelIds<TData>()
        {
            try
            {
                var hotelIds = StaticData
                   .FirstOrDefault(x => x.SettingKey == SettingKeys.HotelIds)?
                   .Value as List<int> ?? [];

                var entitieData = typeof(TData) == typeof(object)
                    ? (TData)(object)hotelIds!
                    : (TData)Convert.ChangeType(hotelIds, typeof(TData));

                string message = hotelIds.Count > 0 ? "" : "hotelIds are empty";
                var error = hotelIds.Count == 0;
                var result = error
                    ? Error(message, entitieData)
                    : Ok(entitieData, message);

                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return ErrorDefault<TData>(ex);
            }
        }

        public async Task<OptimaResult<TData>> GetHotelCodes<TData>()
        {
            try
            {
                var hotelIds = StaticData
                   .FirstOrDefault(x => x.SettingKey == SettingKeys.HotelCodes)?
                   .Value as List<string> ?? [];

                var entitieData = typeof(TData) == typeof(object)
                    ? (TData)(object)hotelIds!
                    : (TData)Convert.ChangeType(hotelIds, typeof(TData));

                string message = hotelIds.Count > 0 ? "" : "HotelCodes are empty";
                var error = hotelIds.Count == 0;
                var result = error
                    ? Error(message, entitieData)
                    : Ok(entitieData, message);

                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return ErrorDefault<TData>(ex);
            }
        }

        public async Task<OptimaResult<TData>> GetHotels<TData>(List<int> customerIds)
        {
            try
            {
                var entities = await GetHotels(customerIds);
                var entitieData = typeof(TData) == typeof(object)
                    ? (TData)(object)entities.Data
                    : (TData)Convert.ChangeType(entities, typeof(TData));
                string message = entities.Message?.Text ?? "";
                var error = entities.Error;
                return error ? Error(message, entitieData) : Ok(entitieData, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<TData>(ex);
            }
        }

        /// <summary> Get Hotels from Optima </summary>
        public async Task<OptimaResult<List<HotelData>>> GetHotels(List<int> customerIds)
        {
            try
            {
                var path = Config.GetSection(SettingKeys.Hotels)?.Path ?? throw new ApiException("The path cannot be null.");
                var requests = MakeRequests(customerIds);
                var results = await SendBulkAsync<HotelData>(ApiHttpClientNames.OptimaMainApi,
                    HttpMethod.Get, path, requests);
                var dataResults = results.SelectMany(x => x.Data ?? []).ToList();
                var message = string.Join(", ", results?.SelectMany(x => x.Message?.Text ?? "") ?? []);
                var error = results?.Any(x => x.Error) ?? false;
                return error ? Error(message, dataResults) : Ok(dataResults);
            }
            catch (Exception ex)
            {
                return ErrorDefault<List<HotelData>>(ex);
            }
        }

        /// <summary> Get Umbraco Hotels from Optima </summary>
        public Task<List<HotelData>> GetUmbHotels()
        {
            try
            {
                var umbHotels = StaticData.GetValueOrDefault<OptimaSettings>(SettingKeys.OptimaSettings)?
                    .CodesConversion
                    .Select(x => int.TryParse(x.HotelCode.G4Code, out var value) ? (int?)value : null)
                    .Where(value => value.HasValue)
                    .Select(value => value!.Value)
                    .ToList() ?? [];

                var optimaHotels = StaticData.GetValueOrDefault<List<HotelData>>(SettingKeys.Hotels)?
                    .Where(x => umbHotels?.Contains(x.HotelID) ?? false)
                    .ToList();

                return Task.FromResult(optimaHotels);
            }
            catch (Exception ex)
            {
                return Task.FromResult(ErrorDefault<List<HotelData>>(ex).Data);
            }
        }

        /// <summary> Get Hotels from Optima </summary>
        public async Task<OptimaResult<List<HotelData>>> GetHotelCodes()
        {
            try
            {
                var hotels = (Config.GetSections(SettingKeys.StaticData).ToDataList()
                   .Where(x => x.SettingKey == SettingKeys.HotelCodes)?
                   .FirstOrDefault()?
                   .Value as List<string>)?
                   .Select(x => new HotelData() { HotelCode = x })
                   .ToList();

                return Ok(hotels ?? []);
            }
            catch (Exception ex)
            {
                return ErrorDefault<List<HotelData>>(ex.InnerException?.Message ?? ex.Message);
            }
        }

        /// <summary> Get Clerk from Optima </summary>
        public async Task<OptimaResult<List<ClerkData>>> GetClerkAsync(int hotelId, string promoCode)
        {
            try
            {
                var path = Config.GetSection(SettingKeys.StaticData, SettingKeys.Clerks)?.Path ?? throw new ApiException("Clerks path cannot be null.");
                var HotelIDList = await GetUmbHotels();
                var clerksRequest = new
                {
                    promoCode,
                    hotelID = hotelId,
                    userName = AppSettings.Optima.UserName,
                    password = AppSettings.Optima.Password,
                    customerId = 1
                };

                var clerkDataResult = await SendAsync<List<ClerkData>>(ApiHttpClientNames.OptimaCustomerApi,
                    HttpMethod.Get, path, clerksRequest);

                string message = clerkDataResult.Message?.Text ?? "";
                var error = clerkDataResult.Error;
                return error ? Error(message, clerkDataResult.Data) : Ok(clerkDataResult.Data, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<List<ClerkData>>(ex);
            }
        }

        /// <summary> Get Clerks from Optima </summary>
        public async Task<OptimaResult<List<ClerkData>>> GetClerksAsync(List<int> hotelsIds)
        {
            try
            {
                var path = Config.GetSection(SettingKeys.Clerks)?.Path ?? throw new ApiException("Clerks path cannot be null.");
                var requests = MakeRequests([1], hotelsIds);
                var results = await SendBulkAsync<ClerkData>(
                    ApiHttpClientNames.OptimaMainApi, HttpMethod.Get, path,
                    requests);
                var dataResults = results.SelectMany(x => x.Data ?? []).ToList();
                var message = string.Join(", ", results.SelectMany(x => x.Message?.Text ?? ""));
                var error = results.Any(x => x.Error);
                return error ? Error(message, dataResults) : Ok(dataResults);
            }
            catch (Exception ex)
            {
                return await Task.FromResult(ErrorDefault<List<ClerkData>>(ex));
            }
        }

        /// <summary> Get Clerks from Optima </summary>
        public async Task<OptimaResult<TData>> GetClerksAsync<TData>(List<int> hotelsIds)
        {
            try
            {
                var entities = await GetClerksAsync(hotelsIds);
                var entitieData = typeof(TData) == typeof(object)
                    ? (TData)(object)entities.Data!
                    : (TData)Convert.ChangeType(entities, typeof(TData));
                string message = entities?.Message?.Text ?? "";
                var error = entities?.Error ?? false;
                return error ? Error(message, entitieData) : Ok(entitieData, message);
            }
            catch (Exception ex)
            {
                return await Task.FromResult(ErrorDefault<TData>(ex));
            }
        }

        /// <summary> Get PriceCodeTranslations </summary>
        public async Task<OptimaResult<TData>> GetPriceCodeTranslations<TData>(List<int> customerIds)
        {
            try
            {
                var entities = await GetPriceCodeTranslations(customerIds);
                var entitieData = typeof(TData) == typeof(object)
                    ? (TData)(object)entities.Data!
                    : (TData)Convert.ChangeType(entities, typeof(TData));
                string message = entities?.Message?.Text ?? "";
                var error = entities?.Error ?? false;
                return error ? Error(message, entitieData) : Ok(entitieData, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<TData>(ex);
            }
        }

        /// <summary> Get PriceCodeTranslations </summary>
        public async Task<OptimaResult<List<PriceCodeTranslationsData>>> GetPriceCodeTranslations(List<int> customerIds)
        {
            try
            {
                var path = Config.GetSection(SettingKeys.PriceCodeTranslations)?.Path ?? throw new ApiException("Clerks path cannot be null.");
                var requests = MakeRequests(customerIds);
                var results = await SendBulkAsync<PriceCodeTranslationsData>(
                    ApiHttpClientNames.OptimaMainApi, HttpMethod.Get, path,
                    requests);
                var dataResults = results.SelectMany(x => x.Data ?? []).ToList();
                var message = string.Join(", ", results.SelectMany(x => x.Message?.Text ?? ""));
                var error = results.Any(x => x.Error);
                return error ? Error(message, dataResults) : Ok(dataResults);
            }
            catch (Exception ex)
            {
                return ErrorDefault<List<PriceCodeTranslationsData>>(ex);
            }
        }

        /// <summary> Get PriceCodeCategoryTranslations </summary>
        public async Task<OptimaResult<TData>> GetPriceCodeCategoryTranslations<TData>(List<int> customerIds)
        {
            try
            {
                var entities = await GetPriceCodeCategoryTranslations(customerIds);
                var entitieData = typeof(TData) == typeof(object)
                    ? (TData)(object)entities.Data!
                    : (TData)Convert.ChangeType(entities, typeof(TData));
                string message = entities?.Message?.Text ?? "";
                var error = entities?.Error ?? false;
                return error ? Error(message, entitieData) : Ok(entitieData, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<TData>(ex);
            }
        }

        /// <summary> Get PriceCodeCategoryTranslations </summary>
        public async Task<OptimaResult<List<PriceCodeCategoryTranslationsData>>> GetPriceCodeCategoryTranslations(List<int> customerIds)
        {
            try
            {
                var path = Config.GetSection(SettingKeys.PriceCodeCategoriesTranslations)?.Path ?? throw new ApiException("PriceCodeCategories path cannot be null.");
                var requests = MakeRequests(customerIds);
                var results = await SendBulkAsync<PriceCodeCategoryTranslationsData>(ApiHttpClientNames.OptimaMainApi,
                    HttpMethod.Get, path, requests);
                var dataResults = results.SelectMany(x => x.Data ?? []).ToList();
                var message = string.Join(", ", results.SelectMany(x => x.Message?.Text ?? ""));
                var error = results.Any(x => x.Error);
                return error ? Error(message, dataResults) : Ok(dataResults);
            }
            catch (Exception ex)
            {
                return ErrorDefault<List<PriceCodeCategoryTranslationsData>>(ex);
            }
        }

        /// <summary> Get PriceCodeCategoryLinks </summary>
        public async Task<OptimaResult<TData>> GetPriceCodeCategoryLinks<TData>(List<int> customerIds)
        {
            try
            {
                var entities = await GetPriceCodeCategoryLinks(customerIds);
                var entitieData = typeof(TData) == typeof(object)
                    ? (TData)(object)entities.Data!
                    : (TData)Convert.ChangeType(entities, typeof(TData));
                string message = entities.Message?.Text ?? "";
                var error = entities.Error;
                return error ? Error(message, entitieData) : Ok(entitieData, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<TData>(ex);
            }
        }

        /// <summary> Get PriceCodeCategoryLinks </summary>
        public async Task<OptimaResult<List<PriceCodeCategoryLinkData>>> GetPriceCodeCategoryLinks(List<int> customerIds)
        {
            try
            {
                var path = Config.GetSection(SettingKeys.PriceCodeCategoryLinks)?.Path!;
                var requests = MakeRequests(customerIds);
                var results = await SendBulkAsync<PriceCodeCategoryLinkData>(ApiHttpClientNames.OptimaMainApi,
                    HttpMethod.Post, path, requests);
                var dataResults = results.SelectMany(x => x.Data ?? []).ToList();
                var message = string.Join(", ", results.SelectMany(x => x.Message?.Text ?? ""));
                var error = results.Any(x => x.Error);
                return error ? Error(message, dataResults) : Ok(dataResults);
            }
            catch (Exception ex)
            {
                return ErrorDefault<List<PriceCodeCategoryLinkData>>(ex);
            }
        }

        /// <summary> Get PackageTranslations </summary>
        public async Task<OptimaResult<TData>> GetPackageTranslations<TData>(List<int> customerIds)
        {
            try
            {
                var entities = await GetPackageTranslations(customerIds);
                var entitieData = typeof(TData) == typeof(object)
                    ? (TData)(object)entities.Data!
                    : (TData)Convert.ChangeType(entities, typeof(TData));
                string message = entities.Message?.Text ?? "";
                var error = entities.Error;
                return error ? Error(message, entitieData) : Ok(entitieData, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<TData>(ex);
            }
        }

        /// <summary> Get PackageTranslations </summary>
        public async Task<OptimaResult<List<PackageTranslationsData>>> GetPackageTranslations(List<int> customerIds)
        {
            try
            {
                var path = Config.GetSection(SettingKeys.StaticData, SettingKeys.PackageTranslations)?.Path!;
                var requests = customerIds.Select(x =>
                    new OptimaRequest { CustomerID = x, UserName = OptimaBaseRequest.UserName, Password = OptimaBaseRequest.Password })
                    .ToArray();
                var packageTranslationsResults = await SendBulkAsync<PackageTranslationsData>(
                    ApiHttpClientNames.OptimaMainApi, HttpMethod.Get, path, requests);
                var packageTranslationsResult = packageTranslationsResults.FirstOrDefault();
                var packageTranslationsData = packageTranslationsResults.SelectMany(x => x.Data!).ToList() ?? [];
                var message = packageTranslationsResult?.Message?.Text;
                var error = packageTranslationsResult?.Error ?? false;
                return error ? Error(message, packageTranslationsData) : Ok(packageTranslationsData, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<List<PackageTranslationsData>>(ex);
            }
        }

        /// <summary> Get PackageTranslations </summary>
        public async Task<OptimaResult<List<PackageTranslationsData>>> GetPackageTranslations(TranslationsPackageRequest request)
        {
            try
            {
                OptimaResult<List<PackageTranslationsData>> result;

                var postResult = await OptimaMainApi.PostAsync<TranslationsPackageRequest, OptimaResult<List<PackageTranslationsData>>>(
                        AppSettings.Optima.TranslationsPackageUrl, request);

                if (postResult == null || !postResult.IsSuccess)
                {
                    var errMessage = ApiHelper.CleanSymbols(postResult?.Message) ?? "Unexpected error";
                    result = Error<List<PackageTranslationsData>>($"{ApiHelper.LogTitle()} failed. Code: {errMessage}");
                }
                if (postResult != null && postResult.IsSuccess && postResult.Value != null && !postResult.Value.IsSuccess)
                {
                    var errMessage = ApiHelper.CleanSymbols(postResult?.Value?.Message!.Text) ?? "Unexpected error";
                    result = Error<List<PackageTranslationsData>>($"{ApiHelper.LogTitle()} failed. Code: {errMessage}");
                }
                else if (postResult?.Value?.Data == null)
                {
                    result = NoContent<List<PackageTranslationsData>>("No data");
                }

                result = postResult?.Value ?? new();
                result.RequestUrl = postResult?.RequestUrl;
                result.RequestData = request;
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("{TITLE}. Code: {MESSAGE}", ex.InnerException?.Message ?? ex.Message);
                return ErrorDefault<List<PackageTranslationsData>>(ex);
            }
        }

        public async Task<OptimaResult<TData>> GetPolicies<TData>(List<int> customerIds)
        {
            try
            {
                var entities = await GetPolicies(customerIds);
                var entitieData = typeof(TData) == typeof(object)
                    ? (TData?)(object?)entities.Data
                    : (TData)Convert.ChangeType(entities, typeof(TData));
                var message = entities.Message?.Text ?? "error";
                return entities.Error ? Error(message, entitieData) : Ok(entitieData, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<TData>(ex);
            }
        }

        public async Task<OptimaResult<List<PolicyData>>> GetPolicies(List<int> customerIds)
        {
            try
            {
                var path = Config.GetSection(SettingKeys.Policy)?.Path ?? throw new ApiException("The path cannot be null.");
                var requests = MakeRequests(customerIds);
                var results = await SendBulkAsync<PolicyData>(ApiHttpClientNames.OptimaMainApi,
                    HttpMethod.Get, path, requests);
                var dataResults = results.SelectMany(x => x.Data ?? []).ToList();
                var message = string.Join(", ", results?.SelectMany(x => x.Message?.Text ?? "") ?? []);
                var error = results?.Any(x => x.Error) ?? false;
                return error ? Error(message, dataResults) : Ok(dataResults, message);
            }
            catch (Exception ex)
            {
                return ErrorDefault<List<PolicyData>>(ex);
            }
        }

        #endregion public methods

        private OptimaRequest[] MakeRequests(List<int> customerIds, List<int>? hotelIds = null, bool? isLocal = null)
        {
            return customerIds.Select(x =>
                new OptimaRequest
                {
                    HotelIDList = hotelIds,
                    CustomerID = x,
                    IsLocal = isLocal,
                    UserName = OptimaBaseRequest.UserName,
                    Password = OptimaBaseRequest.Password
                })
            .ToArray();
        }
    }
}