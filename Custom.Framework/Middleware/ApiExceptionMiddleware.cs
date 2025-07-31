using Custom.Framework.Configuration.Models;
using Custom.Framework.Exceptions;
using Custom.Framework.Extensions;
using Custom.Framework.Models;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace Custom.Framework.Middleware
{
    public class ApiExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public ApiExceptionMiddleware(RequestDelegate next, ILogger logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Invokes the middleware.
        /// </summary>
        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        /// <summary>
        /// Handles the exception and generates the appropriate response.
        /// </summary>
        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            ApiErrorDetails result;
            context.Response.ContentType = "application/json";
            string title;
            var requestUrl = context.GetRequestFullPath();
            var requestData = context.GetRequestData(HttpContextItemsKeys.RequestData);

            if (exception is ApiException apiException)
            {
                title = Path.GetFileNameWithoutExtension(apiException.CallerFilePath ?? "");
                result = new ApiErrorDetails()
                {
                    Message = $"{apiException.Message}.",
                    StatusCode = apiException.StatusCode,
                    RequestUrl = requestUrl,
                    RequestData = requestData
                };
                context.Response.StatusCode = (int)apiException.StatusCode;
            }
            else
            {
                title = "Application ErrorInfo.";
                result = new ApiErrorDetails()
                {
                    Message = $"{HttpStatusCode.InternalServerError}. MESSAGE: {exception.Message}.",
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    RequestUrl = requestUrl,
                    RequestData = requestData
                };
                context.Response.StatusCode = result.StatusCode;
            }

            _logger.Error("{TITLE} Message: {MESSAGE}. \nRequestUrl: {REQUESTURL}. \nRequest: {REQUESTDATA}. \nStackTrace: {STACKTRACE}",
                    title, result.Message, requestUrl, requestData, exception.StackTrace);

            await context.Response.WriteAsync(result.ToString());
        }
    }


}
