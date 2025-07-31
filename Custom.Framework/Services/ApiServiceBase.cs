using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Exceptions;
using Custom.Framework.Extensions;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Custom.Framework.StaticData.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog.Events;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Custom.Framework.Services
{
    /// <summary> Base class for all service's instances </summary>
    public class ApiServiceBase(IHttpContextAccessor httpContextAccessor)
    {
        #region private fields

        private ILogger? _activityTrace;
        private IMeterFactory? _meterFactory;
        private ILogger? _logger;
        private ApiSettings? _appSettings;
        private IRedisCache? _redisCache;
        private IBlobStorage? _blobStorage;
        private IMemoryCache? _memoryCache;
        private IApiHttpClientFactory? _httpClientFactory;
        private IApiCacheOptions? _apiMemoryCacheOptions;
        private IConfiguration? _configuration;
        private IStaticDataService? _staticDataService;
        private ActivitySource? _activitySource;
        private readonly Counter<int>? _counter;

        #endregion private fields

        #region props

        public HttpContext? HttpContext => httpContextAccessor.HttpContext;

        protected Activity? CurrentActivity => Activity.Current;

        protected ApiSettings AppSettings => _appSettings ??= GetService<IOptions<ApiSettings>>().Value;

        protected string RequestUrl => HttpContext?.Request.GetDisplayUrl() ?? $"{HttpContext?.Request.Host}{HttpContext?.Request.Path}";

        // Todo: remove this and log the needed things using ILogger<T>
        //protected ILogger ActivityTrace => _activityTrace ??= GetService<ILogger>(typeof(ApiActivityLogger).Name)!;

        protected ILogger Logger => _logger ??= GetService<ILogger>();

        protected IApiHttpClientFactory HttpClientFactory => _httpClientFactory ??= GetService<IApiHttpClientFactory>();

        protected IConfiguration Configuration => _configuration ??= GetService<IConfiguration>();

        protected IStaticDataService StaticData => _staticDataService ??= GetService<IStaticDataService>();

        protected IRedisCache RedisCache => _redisCache ??= GetService<IRedisCache>();

        protected IBlobStorage BlobStorage => _blobStorage ??= GetService<IBlobStorage>();

        protected IMemoryCache MemoryCache => _memoryCache ??= GetService<IMemoryCache>();

        private IMeterFactory MeterFactory => _meterFactory ??= GetService<IMeterFactory>();

        protected IApiCacheOptions ApiMemoryCacheOptions => _apiMemoryCacheOptions ??= GetService<IApiCacheOptions>();

        #endregion props

        #region service results

        /// <summary> ServiceResult for Ok result </summary>
        public IServiceResult<T> Ok<T>(T? data = default, string message = "Success", int status = 200)
            => Result(LogEventLevel.Information, data, status, message);

        /// <summary> ServiceResult for NoContent result </summary>
        public IServiceResult<T> NotFound<T>(string message = "Not Found")
           => Result<T>(LogEventLevel.Warning, default, 404, message);

        /// <summary> ServiceResult for NoContent result </summary>
        public IServiceResult<T> NoContent<T>(string message = "No Content")
           => Result<T>(LogEventLevel.Warning, default, 204, message);

        public IServiceResult<T> NoData<T>(T? data = default, string message = "No Data")
           => Result(LogEventLevel.Error, data, 204, message);

        /// <summary> ServiceResult for BadRequest result </summary>
        public IServiceResult<T> BadRequest<T>(string message = "Bad Request")
            => Result<T>(LogEventLevel.Error, default, 400, message);

        /// <summary> ServiceResult for Warning result </summary>
        public IServiceResult<T> Warning<T>(string message = "Warning")
            => Result<T>(LogEventLevel.Warning, default, 199, message);

        /// <summary> ServiceResult for error result </summary>
        public IServiceResult<T> Error<T>(string message, int status = 500)
            => Result<T>(LogEventLevel.Error, default, status, message);

        public IServiceResult<T> Error<T>(T? data = default, string message = "error")
            => Result(LogEventLevel.Error, data, 400, message);

        private IServiceResult<T> Result<T>(LogEventLevel logEvent, T? data, int status, string message = "", Dictionary<string, object>? genericParameters = default)
        {
            var correlationId = HttpContext?.GetOrAddCorrelationHeader();
            var requestUrl = HttpContext?.GetRequestFullPath();
            var requestData = HttpContext?.Items.ContainsKey(HttpContextItemsKeys.RequestData) == true
                ? HttpContext.Items[HttpContextItemsKeys.RequestData]?.ToString()
                : string.Empty;

            var isDebugMode = HttpContext?.IsRequestDebugMode() ?? false;
            if (isDebugMode)
            {
                CurrentActivity?.SetStatus(status == 200 ? ActivityStatusCode.Ok : ActivityStatusCode.Error, message);
                CurrentActivity?.AddEvent(new ActivityEvent(logEvent.ToString(),
                    tags: new ActivityTagsCollection
                    {
                        { $"{CurrentActivity.DisplayName}_name", ApiHelper.ServiceName },
                        { $"{CurrentActivity.DisplayName}_controller", ApiHelper.ServiceName },
                        { $"{CurrentActivity.DisplayName}_request_url", requestUrl },
                        { $"{CurrentActivity.DisplayName}_request_type", typeof(T).FullName },
                        { $"{CurrentActivity.DisplayName}_request_data", requestData },
                        { $"{CurrentActivity.DisplayName}_result_status", status },
                        { $"{CurrentActivity.DisplayName}_result_message", message },
                        { $"{CurrentActivity.DisplayName}_result_data", status != 200 ? data : "" },
                    }));
            }

            Logger.Write(logEvent, "{TITLE} Code: {MESSAGE}. \nRequestUrl: {STATUS}. \nRequest: {REQUEST}",
                ApiHelper.ServiceName, message, requestUrl, requestData);

            var serviceResult = new ServiceResult<T>(message, data, status)
            {
                RequestUrl = requestUrl ?? RequestUrl,
                RequestData = requestData,
                GenericParameters = genericParameters,
                CorrelationId = correlationId ?? string.Empty
            };

            return serviceResult;
        }

        private IServiceResult<T> Result<T>(LogEvent logEvent, T data, int status, string message = "")
        {
            var requestUrl = HttpContext?.GetRequestFullPath();
            var requestData = HttpContext?.Items[HttpContextItemsKeys.RequestData]?.ToString();

            Logger.Write(logEvent);

            return new ServiceResult<T>(message, data, status)
            {
                RequestUrl = requestUrl ?? RequestUrl,
                RequestData = requestData
            };
        }

        #endregion service results

        #region protected methods

        /// <summary> GetService go to get the instance of TFilterType from registered services </summary>
        protected T GetService<T>(object? serviceKey = null)
        {
            object? service = default;

            var serviceProvider = HttpContext?.RequestServices;
            if (serviceProvider != null)
            {
                service = (serviceKey != null)
                    ? serviceProvider.GetKeyedService<T>(serviceKey)!
                    : serviceProvider.GetService(typeof(T));
            }

            return service != null
                ? (T)service
                : throw new ApiException(ServiceStatus.FatalError, $"{typeof(T).Name} not registered");

        }

        protected TResult GetService<T, TResult>(object? serviceKey = null)
        {
            object service;

            ApiThrowHelper.ThrowIfNull(HttpContext, new ApiException(ServiceStatus.FatalError, "HttpContext not defined"));

            if (serviceKey != null)
            {
                return (TResult)(object)HttpContext.RequestServices.GetKeyedService<T>(serviceKey)!;
            }

            service = HttpContext.RequestServices.GetService(typeof(T))
                ?? throw new ApiException(ServiceStatus.FatalError,
                   $"{nameof(GetService)} error: {typeof(T).Name} not registered");

            return (TResult)Convert.ChangeType(service, typeof(TResult));
        }

        #endregion protected methods 
    }
}