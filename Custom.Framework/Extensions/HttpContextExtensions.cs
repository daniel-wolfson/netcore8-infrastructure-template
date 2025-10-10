using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Custom.Framework.Extensions
{
    public static class HttpContextExtensions
    {
        /// <summary>
        /// Gets the request data stored in the HttpContext items.
        /// </summary>
        public static string? GetRequestData(this HttpContext httpContext, string itemName = "RequestData")
        {
            if (httpContext.Items.ContainsKey(itemName))
            {
                return httpContext.Items[itemName]?.ToString();
            }
            return null;
        }

        /// <summary>
        /// Get requestBody from HttpContext
        /// </summary>
        public static async Task<string> GetRequestBody(this HttpContext context)
        {
            string requestBody = string.Empty;

            if (context.Request.Method == HttpMethods.Post ||
                context.Request.Method == HttpMethods.Put ||
                context.Request.Method == HttpMethods.Patch)
            {
                // Enable seeking on the request body stream
                context.Request.EnableBuffering();

                // Read the request body to a string
                requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
                context.Items.TryAdd(HttpContextItemsKeys.RequestData,
                    Regex.Replace(requestBody.Replace("\n", ""), @"\s+", ""));

                // Reset the request body stream position to the beginning
                context.Request.Body.Position = 0;
            }

            return requestBody;
        }

        /// <summary>
        /// Gets the controller and action name from the HttpContext.
        /// </summary>
        public static (string controllerName, string actionName) GetContextData(this HttpContext context)
        {
            string controllerName = string.Empty;
            string actionName = string.Empty;
            var endpoint = context.GetEndpoint();

            if (endpoint != null)
            {
                var controllerActionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
                if (controllerActionDescriptor != null)
                {
                    controllerName = controllerActionDescriptor.ControllerName;
                    actionName = controllerActionDescriptor.ActionName;
                }
            }
            return (controllerName, actionName);
        }

        /// <summary>
        /// Gets the correlation header from the HttpContext Request.
        /// </summary>
        public static string GetOrAddCorrelationHeader(this HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (string.IsNullOrWhiteSpace(context.Request.Headers[RequestHeaderKeys.CorrelationId]))
                context.Request.Headers[RequestHeaderKeys.CorrelationId] = Guid.NewGuid().ToString();

            return context.Request.Headers[RequestHeaderKeys.CorrelationId]!;
        }

        /// <summary>
        /// IsRequestDebugMode - Gets the value of the IsDebugMode from the HttpContext Request.
        /// </summary>
        public static bool IsRequestDebugMode(this HttpContext httpContext)
        {
            try
            {
                if (httpContext != null)
                {
                    bool isDebugMode = false;
                    if (httpContext.Items!.TryGetValue("IsDebugMode", StringComparison.CurrentCultureIgnoreCase, out object? debugModeValue1))
                    {
                        _ = bool.TryParse(debugModeValue1?.ToString(), out isDebugMode);
                    }
                    else if (httpContext.Request.Query.TryGetValue("IsDebugMode", StringComparison.CurrentCultureIgnoreCase, out var debugModeValue2) &&
                        string.Equals(debugModeValue2, "true", StringComparison.CurrentCultureIgnoreCase))
                    {
                        _ = bool.TryParse(debugModeValue2, out isDebugMode);
                    }
                    return isDebugMode;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} exception: {EXCEPTION}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            return false;
        }

        /// <summary>
        /// UseMemorCache - Gets the value of the UseMemorCache from the HttpContext Request.
        /// </summary>
        public static bool UseMemorCache(this HttpContext httpContext)
        {
            try
            {
                if (httpContext != null)
                {
                    bool useMemorCache = false;
                    if (httpContext.Items!.TryGetValue("UseMemorCache", StringComparison.CurrentCultureIgnoreCase, out object? debugModeValue1))
                    {
                        _ = bool.TryParse(debugModeValue1?.ToString(), out useMemorCache);
                    }
                    else if (httpContext.Request.Query.TryGetValue("UseMemorCache", StringComparison.CurrentCultureIgnoreCase, out var debugModeValue2) &&
                        string.Equals(debugModeValue2, "true", StringComparison.CurrentCultureIgnoreCase))
                    {
                        _ = bool.TryParse(debugModeValue2, out useMemorCache);
                    }
                    return useMemorCache;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} exception: {EXCEPTION}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            return false;
        }

        /// <summary>
        /// GetMemorCacheTTL - Gets the value of the MemorCacheTTL from the HttpContext Request.
        /// </summary>
        public static int? GetMemorCacheTTL(this HttpContext httpContext)
        {
            // memorCacheTTL - default 10 min., it must be redefine
            // from httpContext.Items or httpContext.Request.Query
            int memorCacheTTL = 0;
            try
            {
                if (httpContext != null)
                {
                    if (httpContext.Items!.TryGetValue("MemorCacheTTL", StringComparison.CurrentCultureIgnoreCase, out object? memorCacheTTL1))
                    {
                        memorCacheTTL = memorCacheTTL1 is int ttl ? ttl : memorCacheTTL;
                    }
                    else if (httpContext.Request.Query.TryGetValue("MemorCacheTTL", StringComparison.CurrentCultureIgnoreCase, out var memorCacheTTL2))
                    {
                        _ = int.TryParse(memorCacheTTL2, out memorCacheTTL);
                    }
                    return memorCacheTTL == 0 ? null : memorCacheTTL;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} setting exception: {EXCEPTION}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }

            return memorCacheTTL;
        }

        public static bool TryGetValue(this IQueryCollection query, string key,
            StringComparison comparison, out StringValues value)
        {
            foreach (var k in query.Keys)
            {
                if (string.Equals(k, key, comparison))
                {
                    value = query[k];
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryGetValue(this IDictionary<object, object> items, string key, StringComparison comparison, out object? value)
        {
            foreach (var itemKey in items.Keys)
            {
                if (itemKey is string strKey && string.Equals(strKey, key, comparison))
                {
                    value = items[itemKey];
                    return true;
                }
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Gets the value of the specified request header.
        /// </summary>
        public static string? GetRequestHeader(this HttpContext httpContext, string headerName)
        {
            return httpContext.Request.Headers[headerName].ToString();
        }

        /// <summary>
        /// Gets the request token from the Authorization header.
        /// </summary>
        public static string GetRequestToken(this HttpContext httpContext)
        {
            string authHeader = httpContext.Request.Headers["Authorization"].ToString();
            return authHeader?.Replace("Bearer ", "") ?? string.Empty;
        }

        /// <summary>
        /// Gets the Endpoint information for the current request.
        /// </summary>
        /// <param name="context">The HttpContext.</param>
        /// <returns>The Endpoint information.</returns>
        public static Endpoint? GetEndpointInfo(this HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return context.Features.Get<IEndpointFeature>()?.Endpoint;
        }

        /// <summary>
        /// Gets the user data from the request token.
        /// </summary>
        public static string GetRequestUserData(this HttpContext httpContext)
        {
            string result = string.Empty;
            var handler = new JwtSecurityTokenHandler();
            string authHeader = httpContext.Request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authHeader))
            {
                var SecurityToken = handler.ReadToken(authHeader) as JwtSecurityToken;
                result = SecurityToken.Claims.First(claim => claim.Type == ClaimTypes.UserData).Value;
            }
            return result;
        }

        /// <summary>
        /// Gets the return type of the action method.
        /// </summary>
        public static Type? GetActionReturnType(this HttpContext context)
        {
            Type? responseDeclaredType = null;
            var endpoint = context.GetEndpointInfo();

            if (endpoint != null)
            {
                var controllerActionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
                if (controllerActionDescriptor != null)
                {
                    responseDeclaredType = controllerActionDescriptor.MethodInfo.ReturnType;

                    if (controllerActionDescriptor.MethodInfo.ReturnType.IsGenericType)
                    {
                        if (controllerActionDescriptor.MethodInfo.ReturnType.GetGenericTypeDefinition() == typeof(ActionResult<>))
                        {
                            responseDeclaredType = controllerActionDescriptor.MethodInfo.ReturnType.GetGenericArguments()[0];
                        }
                    }
                }
            }

            return responseDeclaredType;
        }

        /// <summary>
        /// Gets the URL of the current request.
        /// </summary>
        public static string GetRequestUrl(this HttpContext context)
        {
            return $"{context.Request.Scheme}://{context.Request.Host.Value}{context.Request.Path}{context.Request.QueryString}";
        }

        /// <summary>
        /// Gets the full path of the current request.
        /// </summary>
        public static string? GetRequestFullPath(this HttpContext httpContext)
        {
            return httpContext.Request.GetDisplayUrl();
        }

        /// <summary>
        /// Gets the response body as a byte array.
        /// </summary>
        public static Task<byte[]> GetResponse(this HttpContext context)
        {
            var responseStream = context.Response.Body;

            using (var buffer = new MemoryStream())
            {
                try
                {
                    context.Response.Body = buffer;
                }
                finally
                {
                    context.Response.Body = responseStream;
                }

                if (buffer.Length == 0)
                    return Task.FromResult(Array.Empty<byte>());

                var bytes = buffer.ToArray();
                responseStream.Write(bytes, 0, bytes.Length);
                return Task.FromResult(bytes);
            }
        }

        /// <summary>
        /// Converts the HttpRequestMessage to an error IServiceResult.
        /// </summary>
        public static IServiceResult<TResult> ToErrorServiceResult<TResult>(this HttpRequestMessage requestMessage, object data, string message)
        {
            var serviceResult = ServiceResult<TResult>.Error(message);
            serviceResult.RequestUrl = requestMessage?.RequestUri?.ToString() ?? "";
            serviceResult.RequestData = data;
            return serviceResult;
        }

        public static void SetServiceProvider(IServiceProvider services)
        {
            ServicePointManager.ServerCertificateValidationCallback +=
                delegate (object sender,
                    System.Security.Cryptography.X509Certificates.X509Certificate? certificate,
                    System.Security.Cryptography.X509Certificates.X509Chain? chain,
                    System.Net.Security.SslPolicyErrors sslPolicyErrors)
                {
                    return true; // **** Always accept
                };
        }
    }
}
