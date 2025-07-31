using Custom.Framework.Contracts;
using Custom.Framework.Helpers;
using Custom.Framework.Models.Errors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;

namespace Custom.Framework.Models
{
    public struct ServiceResult<T> : IServiceResult<T>
    {
        public static implicit operator ServiceResult<T>(T data) => new("", data, 0);

        public static implicit operator ActionResult<T>(ServiceResult<T> result)
        {
            return result.Status switch
            {
                204 => new NoContentResult(),                  // 204 No Content
                >= 200 and < 300 => new ActionResult<T>((T)result.Value!), // 2xx Success
                400 => new BadRequestObjectResult(result),     // 400 Bad Request
                404 => new NotFoundObjectResult(result),       // 404 Not Found
                401 => new UnauthorizedObjectResult(result),   // 401 Unauthorized
                403 => new ForbidResult(),                     // 403 Forbidden
                409 => new ConflictObjectResult(result),       // 409 Conflict
                _ => new ObjectResult(result) { StatusCode = result.Status }
            };
        }

        public static implicit operator ActionResult(ServiceResult<T> serviceResult)
        {
            if (serviceResult.IsSuccess)
            {
                return new OkObjectResult(serviceResult.Value);
            }
            var result = serviceResult.Status switch
            {
                200 => new OkObjectResult(serviceResult.Message),
                201 => new CreatedResult(serviceResult.Message, serviceResult.Value),
                202 => new AcceptedResult(serviceResult.Message, serviceResult.Value),
                204 => new ObjectResult(serviceResult.Message) { StatusCode = serviceResult.Status },
                400 => new BadRequestObjectResult(serviceResult.Message),
                401 => new UnauthorizedObjectResult(serviceResult.Message),
                404 => new NotFoundObjectResult(serviceResult.Message),
                >= 400 and <= 499 => new ConflictObjectResult(serviceResult.Message),
                >= 500 => new ObjectResult(serviceResult.Message)
                {
                    StatusCode = serviceResult.Status
                },
                _ => new ObjectResult(serviceResult.Message) { StatusCode = serviceResult.Status }
            };
            return result;
        }
        
        public static explicit operator ObjectResult(ServiceResult<T> result)
        {
            var r = new ObjectResult(new
            {
                result.Value,
                result.IsSuccess,
                result.Status,
                result.RequestUrl,
                result.RequestData,
                result.Message,
                result.Content,
                result.GenericParameters,
                result.CorrelationId
            })
            {
                StatusCode = result.Status
            };
            return r;
        }

        #region props

        public T? Value { get; set; }
        public bool IsSuccess { get; set; }
        public int Status { get; set; }
        public string? RequestUrl { get; set; }
        public object? RequestData { get; set; }
        public string Message { get; set; }
        public string CorrelationId { get; set; } = string.Empty;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? Content { get; set; } = null;

        /// <summary> Latency steps  </summary>
        public Dictionary<string, object>? GenericParameters { get; set; }

        #endregion props

        #region ctor

        public ServiceResult(string message) : this(message, default, 200)
        {
        }
        public ServiceResult(string message, T? value, int status = 200,
            Type? dataType = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            if (message == "ErrorDefault")
                Value = MakeDefaultData(value, dataType);
            else
                Value = value;

            Message = message;
            Status = status;
            IsSuccess = Status >= 200 && Status < 300;

            var title = $"{Path.GetFileNameWithoutExtension(callerFilePath)}.{callerMemberName}";
            if (Status > 204 && Status <= 399)
                Serilog.Log.Warning($"{title}: {message} .StatusCode: {status}");
            else if (Status >= 400)
                Serilog.Log.Error($"{title}: {message} .StatusCode: {status}");
        }

        #endregion ctor

        #region public methods

        public static IServiceResult<T> Warning(string message = "Warning")
            => new ServiceResult<T>(message, default, 199);

        public static IServiceResult<T> Ok(T? data = default, string message = "Success", int status = 200)
            => new ServiceResult<T>(message, data, status);

        public static IServiceResult<T> Default(T? data = default)
            => new ServiceResult<T>("ErrorDefault", data, 204);

        public static IServiceResult<T> Default(T? data, Type? dataType = null)
            => new ServiceResult<T>("ErrorDefault", data, 204, dataType);

        public static IServiceResult<T> NoContent(string message = "NoContent", T? data = default)
            => new ServiceResult<T>("ErrorDefault", data, 204) { Message = message };

        public static IServiceResult<T> NoData(string message = "NoData", T? data = default)
            => new ServiceResult<T>("ErrorDefault", data, 204) { Message = message };

        public static IServiceResult<T> Cancel(string? message = null)
           => new ServiceResult<T>(message ?? "service canceled", default, 205);

        public static IServiceResult<T> PartialContent(string? content, string message = "", int status = 206)
            => new ServiceResult<T>(message, default, status) { Content = content };

        public static IServiceResult<T> BadRequest(string message = "BadRequest", T? data = default)
            => new ServiceResult<T>(message, data, 400);

        public static IServiceResult<T> NotFound(string message = "NotFound", T? data = default)
            => new ServiceResult<T>(message, data, 404);

        public static IServiceResult<T> Conflict(Exception? ex)
            => new ServiceResult<T>(ex?.InnerException?.Message ?? ex?.Message ?? "service conflict", default, 409);

        public static IServiceResult<T> Error(Exception? ex)
            => new ServiceResult<T>(ex?.InnerException?.Message ?? ex?.Message ?? "service error", default, 500);

        public static IServiceResult<T> Error(string? message = "service error")
            => new ServiceResult<T>(message ?? "", default, 500);

        public static IServiceResult<T> Error(string? message, T? data)
            => new ServiceResult<T>(message ?? "", data, 500);

        public static IServiceResult<T> Error(IEnumerable<ErrorInfo> errors)
            => new ServiceResult<T>(string.Join(",", errors) ?? "", default, 500);

        public static IServiceResult<T> Error(string? message, int status)
            => new ServiceResult<T>(message ?? "", default, status);

        public static IServiceResult<T> ErrorWithLog(string? message = "service error", T? data = default)
        {
            Serilog.Log.Logger.Error(message ?? "error");
            return new ServiceResult<T>(message ?? "", data, 500);
        }

        // Implicit conversion to IActionResult
        public IActionResult ToActionResult()
        {
            return Status switch
            {
                204 => new NoContentResult(),                  // 204 No Content
                >= 200 and < 300 => new OkObjectResult(this),  // 2xx Success
                400 => new BadRequestObjectResult(this),       // 400 Bad Request
                404 => new NotFoundObjectResult(this),         // 404 Not Found
                401 => new UnauthorizedObjectResult(this),     // 401 Unauthorized
                403 => new ForbidResult(),                     // 403 Forbidden
                409 => new ConflictObjectResult(this),         // 409 Conflict
                _ => new ObjectResult(this) { StatusCode = Status }
            };
        }

        #endregion public methods

        #region private methods

        private T? MakeDefaultData(T? data = default, Type? dataType = null)
        {
            T? result = default;
            try
            {
                if (dataType == null)
                    dataType = typeof(T);

                result = Value ?? (T?)Activator.CreateInstance(dataType);

                if (data == null && !dataType.IsValueType && dataType.IsGenericType)
                {
                    var genericDataType = dataType.GetGenericArguments().FirstOrDefault()?.GetGenericArguments().FirstOrDefault();
                    if (genericDataType != null)
                    {
                        Type dataListType = typeof(List<>).MakeGenericType(genericDataType);
                        var dataList = Activator.CreateInstance(dataListType);
                        var dataProperty = result?.GetType().GetProperty("Data");

                        if (dataProperty != null && dataProperty.CanWrite && dataList != null)
                        {
                            // Set the value of the "Data" property to the created List<TData>
                            dataProperty.SetValue(result, dataList);
                        }
                    }
                }
                else if (data == null && !dataType.IsValueType && !dataType.IsGenericType)
                {
                    result = (T?)Activator.CreateInstance(dataType);
                }
                else
                    result = data;
            }
            catch (Exception ex)
            {
                Serilog.Log.Logger.Error("{TITLE} error: exception {EXCEPTION}",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message);
                //result.Data = data;
            }

            return result;
        }

        #endregion private methods
    }
}