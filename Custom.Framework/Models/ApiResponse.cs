using Newtonsoft.Json;
using System.Net;

namespace Custom.Framework.Models
{
    public class ApiResponse
    {
        public string TraceId { get; set; }
        public string Message { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object Value { get; set; }

        public string RequestUrl { get; set; }

        public Type ActionType { get; set; }

        public string StatusCode { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ApiErrorDetails Error { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IEnumerable<ApiValidationError>? Errors { get; set; }

        public ApiResponse(int statusCode, IEnumerable<ApiValidationError>? validationErrors = null)
        {
            StatusCode = ((HttpStatusCode)statusCode).ToString();
            Errors = validationErrors; //ErrorInfo = new ApiError(400, validationErrors);
        }

        public override string ToString()
        {
            return $"Custom ApiResponse {base.ToString()}:{Environment.NewLine}" +
                   $"TraceId:{TraceId} " +
                   $"RequestUrl: {RequestUrl} " +
                   $"Code: {Value} " +
                   $"Message: {Message} " +
                   $"ErrorInfo Details: {Error} " +
                   $"StatusCode: {StatusCode}";
        }
    }
}
