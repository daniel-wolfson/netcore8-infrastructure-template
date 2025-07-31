using FluentValidation;
using Custom.Domain.Optima.Models.Base;
using Custom.Domain.Optima.Models.Main;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Core;
using Custom.Framework.Extensions;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Custom.Framework.Models.Base;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Serilog.Events;

namespace Custom.Framework.Repositoty
{
    public class OptimaBaseRepository(IHttpContextAccessor httpContextAccessor)
        : ApiBase(httpContextAccessor), IOptimaBaseRepository
    {
        #region private fields

        /// <summary> Hotel IDs, (Warning) It must to be loaded before all static data </summary>
        public List<int> HotelIDList => StaticData
            .GetValueOrDefault<List<HotelData>>(SettingKeys.Hotels)?
            .Select(h => h.HotelID).ToList() ?? [];

        public IApiOptimaDataMapper OptimaMapper => GetService<IApiOptimaDataMapper>();

        #endregion private fields

        #region optima results

        public OptimaResult<TData> Ok<TData>(TData data, string? message = "", int status = 200)
        {
            var result = new OptimaResult<TData>()
            {
                Data = data,
                Message = new OptimaMessage() { Text = message ?? "" }
            };

            if (!string.IsNullOrEmpty(message))
                (result.Message ??= new OptimaMessage()).TextNumber = status;

            if (status != 200)
                (result.Message ??= new OptimaMessage()).TextNumber = status;

            return Result(LogEventLevel.Information, message ?? "", data);
        }

        public OptimaResult<TData> NotFound<TData>(string message = "NotFound")
            => Result<TData>(LogEventLevel.Warning, message);

        public OptimaResult<TData> NoContent<TData>(string message = "NoContent")
        {
            var result = Result<TData>(LogEventLevel.Warning, message);
            result.Empty = true;
            return result;
        }

        public OptimaResult<TData> NoData<TData>(string message = "NoData", TData? dataDefault = default)
        {
            var result = Result(LogEventLevel.Warning, message, dataDefault);
            result.Empty = true;
            return result;
        }

        public OptimaResult<TData> Warning<TData>(string message = "Warning")
            => Result<TData>(LogEventLevel.Warning, message);

        public OptimaResult<TData> BadRequest<TData>(string message = "BadRequest")
        {
            var res = Result<TData>(LogEventLevel.Error, message);
            res.Error = true;
            return res;
        }

        public OptimaResult<TData> Error<TData>(string? message = "ErrorInfo", TData? data = default)
        {
            var res = Result<TData>(LogEventLevel.Error, message ?? "ErrorInfo")!;
            res.Error = true;
            res.Data = data ?? typeof(TData).GetDefault<TData?>();
            return res;
        }

        public OptimaResult<TData> ErrorDefault<TData>(string message = "ErrorDefault", TData? dataDefault = default)
        {
            var result = Result(LogEventLevel.Error, message, dataDefault ?? typeof(TData).GetDefault<TData>());
            result.Empty = true;
            return result;
        }

        public OptimaResult<TData> ErrorDefault<TData>(Exception ex, TData? dataDefault = default)
        {
            try
            {
                var title = ApiHelper.GetMethodNameFromStackTrace(ex.InnerException?.StackTrace ?? "");
                Logger.Error("{TITLE}. Code: {MESSAGE}. StackTrace: {STACKTRACE}",
                    ApiHelper.LogTitle(title), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            catch { }

            var result = Result(LogEventLevel.Error,
                ex.InnerException?.Message ?? ex.Message,
                dataDefault ?? typeof(TData).GetDefault<TData>());
            result.Empty = true;

            return result;
        }

        private OptimaResult<TData> Result<TData>(LogEventLevel logEvent, string message = "", TData? data = default)
        {
            try
            {
                data = data == null ? (TData)Activator.CreateInstance(typeof(TData))! : data;

                var result = new OptimaResult<TData>
                {
                    Message = !string.IsNullOrEmpty(message) ? new OptimaMessage() { Text = message } : default,
                    Data = data
                };

                if (HttpContext != null)
                {
                    object? requestData = default;
                    //data = JsonConvert.DeserializeObject<TData>(requestData?.ToString() ?? "") ?? data;
                    HttpContext?.Items.TryGetValue(HttpContextItemsKeys.RequestData, out requestData);
                    result.RequestData = requestData;
                    result.RequestUrl = HttpContext?.GetRequestFullPath();
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Warning("{TITLE} exception: {MESSAGE}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                return typeof(TData).GetDefault<OptimaResult<TData>>();
            }
        }

        #endregion optima results

        #region public methods

        public async Task<OptimaResult<TData>> SendAsync<TData>(string apiHttpClientName,
            HttpMethod httpMethod, string path, object? request)
        {
            OptimaMessage? optimaMessage = default;
            var httpClientFactory = GetService<IApiHttpClientFactory>();
            var optimaApi = httpClientFactory.CreateClient(apiHttpClientName);

            var postResult = await optimaApi.SendAsync<object, OptimaResult<TData>>(httpMethod, path, request);

            if (!postResult.IsSuccess)
            {
                Logger.Error("{TITLE} failed. No data. Url: {URL}. Request: {@REQUEST}",
                    ApiHelper.LogTitle(), $"{optimaApi.BaseAddress}{path}", request);

                postResult = ServiceResult<OptimaResult<TData>>.Error();
            }
            else if (postResult.Value == null)
            {
                Logger.Warning("{TITLE} failed. No data. Url: {URL}. Request: {@REQUEST}",
                    ApiHelper.LogTitle(), $"{optimaApi.BaseAddress}{path}", request);

                postResult = ServiceResult<OptimaResult<TData>>.NoData();
            }
            else if (postResult.Value != null && postResult.Value.Data == null)
            {
                Logger.Warning("{TITLE} failed. No data. Url: {URL}. Request: {@REQUEST}",
                    ApiHelper.LogTitle(), $"{optimaApi.BaseAddress}{path}", request);

                optimaMessage = postResult.Value.Message;
                postResult = ServiceResult<OptimaResult<TData>>.NoData(optimaMessage?.Text ?? "");
            }

            var result = postResult.Value ?? new OptimaResult<TData>();

            if (!typeof(TData).IsValueType)
                result.Data ??= result.Data ?? (TData)Activator.CreateInstance(typeof(TData))!;

            result.RequestUrl = postResult?.RequestUrl ?? "";
            result.RequestData = request;
            result.Message = optimaMessage;
            result.Empty = postResult?.Value == null
                || postResult.Value.Data == null
                || ApiHelper.IsDataNullOrEmpty(postResult.Value.Data);

            return result;
        }

        public async Task<List<OptimaResult<TData>>> SendBulkAsync1<TData>(string apiHttpClientName,
                    HttpMethod httpMethod, string path, OptimaRequest[] requests)
            where TData : List<OptimaData>
        {
            var resultTasks = requests.Select(async request =>
            {
                var data = await SendAsync<TData>(apiHttpClientName, httpMethod, path, request);
                data.Data?.ForEach(x => { x.CustomerId = request.CustomerID; });
                return data;
            }).ToList();

            var results = await Task.WhenAll(resultTasks);

            if (results.Any(x => x.Data == null))
                Logger.Warning("{TITLE} Request to client {CLEINTNAME} returning jsonResult = null", ApiHelper.LogTitle(), apiHttpClientName);

            results = results.Where(x => x.Data != null).ToArray();

            return results.ToList();
        }

        public async Task<List<OptimaResult<List<TData>>>> SendBulkAsync<TData>(string apiHttpClientName,
            HttpMethod httpMethod, string path, OptimaRequest[] requests)
            where TData : OptimaData
        {
            var resultTasks = requests.Select(request =>
            {
                return SendAsync<List<TData>>(apiHttpClientName, httpMethod, path, request).ContinueWith(task =>
                {
                    var data = task.Result;
                    if (data.Data != null)
                    {
                        foreach (var item in data.Data)
                        {
                            item.CustomerId = request.CustomerID;
                        }
                    }
                    return data;
                });
            }).ToList();

            var results = await Task.WhenAll(resultTasks);

            if (results.Any(x => x.Data == null))
                Logger.Warning("{TITLE} Request to client {CLEINTNAME} returning jsonResult = null", ApiHelper.LogTitle(), apiHttpClientName);

            results = results.Where(x => x.Data != null).ToArray();

            return results.ToList();
        }

        public async Task<OptimaResult<TData>> ExecuteAsync<TRequest, TData>(
            ApiHttpClient optimaApi, HttpMethod httpMethod, string url, TRequest request)
            where TData : OptimaData
        {
            var postResult = await optimaApi.SendAsync<TRequest, OptimaResult<TData>>(httpMethod, url, request);

            if (postResult == null || !postResult.IsSuccess)
            {
                var errMessage = postResult?.Message ?? "Unexpected error";
                return Error<TData>($"{ApiHelper.LogTitle()} failed. Code: {errMessage}");
            }
            else if (postResult.IsSuccess && postResult.Value != null && !postResult.Value.IsSuccess)
            {
                var errMessage = postResult?.Value?.Message?.Text ?? "Unexpected error";
                return Error<TData>($"{ApiHelper.LogTitle()} failed. Code: {errMessage}");
            }

            var result = postResult?.Value ?? NoContent<TData>("No data");
            result.RequestUrl = postResult?.RequestUrl;
            result.RequestData = request;

            return result;
        }

        #endregion public methods

        #region private methods

        private static T? GetObjectOrDefault<T>(object? data, Type settingType)
        {
            if (data == null)
                return typeof(T).GetDefault<T>();
            else if (typeof(T) == typeof(string))
                return (T?)Convert.ChangeType(JsonConvert.SerializeObject(data), typeof(T));
            else
                return (T?)Convert.ChangeType(data, settingType);
        }

        #endregion private methods
    }
}