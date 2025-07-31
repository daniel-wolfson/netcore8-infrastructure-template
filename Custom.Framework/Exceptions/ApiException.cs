using Custom.Framework.Models;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;

namespace Custom.Framework.Exceptions
{
    public class ApiException : Exception
    {
        public static ApiException CreateFrom(ServiceStatus statusCode, Exception ex,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
            => new(statusCode, ex, callerFilePath, callerMemberName);

        public static ApiException CreateFrom(Exception ex,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
            => new(ServiceStatus.Conflict, ex, callerFilePath, callerMemberName);

        public ServiceStatus StatusCode { get; set; }
        public string ContentType { get; set; } = @"text/plain";
        public string? RequestUrl { get; set; }
        public object? RequestData { get; set; }
        public string? ResponseData { get; set; }
        public string? CorrelationId { get; set; }
        public string? CallerFilePath { get; set; }
        public string? CallerMemberName { get; set; }

        public ApiException(string message,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
            : base(message)
        {
            StatusCode = ServiceStatus.Conflict;
            CallerFilePath = callerFilePath;
            CallerMemberName = callerMemberName;
        }

        public ApiException(string message, Exception innerException,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
            : base(message)
        {
            StatusCode = ServiceStatus.Conflict;
            CallerFilePath = callerFilePath;
            CallerMemberName = callerMemberName;
        }

        public ApiException(Exception innerException,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
            : base(ServiceStatus.Conflict.ToString(), innerException)
        {
            StatusCode = ServiceStatus.Conflict;
            CallerFilePath = callerFilePath;
            CallerMemberName = callerMemberName;
        }

        public ApiException(
            ServiceStatus statusCode, string message, Exception? innerException = null,
            [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
            : base(message, innerException)
        {
            StatusCode = statusCode;
            CallerFilePath = callerFilePath;
            CallerMemberName = callerMemberName;
        }

        public ApiException(ServiceStatus statusCode, Exception? innerException = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
            : this(statusCode, innerException?.ToString() ?? "")
        {
            CallerFilePath = callerFilePath;
            CallerMemberName = callerMemberName;
        }

        public ApiException(ServiceStatus statusCode, JObject errorObject,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
            : this(statusCode, errorObject.ToString())
        {
            ContentType = @"application/json";
            CallerFilePath = callerFilePath;
            CallerMemberName = callerMemberName;
        }
    }
}
