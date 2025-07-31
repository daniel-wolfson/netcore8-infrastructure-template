using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Exceptions;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Xml;
using ErrorInfo = Custom.Framework.Models.Errors.ErrorInfo;

namespace Custom.Framework.Core
{
    public class ApiHttpClient : IDisposable
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly ApiSettings _apiSettings;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApiHttpClient(HttpClient httpClient, ILogger logger,
            IHttpContextAccessor httpContextAccessor,
            IOptions<ApiSettings> apiSettings)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _httpClient = httpClient;
            _apiSettings = apiSettings.Value;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        #region public props

        public string ClientName { get; set; } = string.Empty;

        public Uri? BaseAddress => _httpClient.BaseAddress;

        public string Path { get; set; } = string.Empty;

        public string ContentType { get; set; } = "application/json";

        public List<ErrorInfo> Errors { get; set; } = [];

        #endregion public props

        #region public methods

        public Task<IServiceResult<string>> GetAsync(string path, CancellationToken? cancellationToken = null)
        {
            return ExecuteAsync<string, string>(HttpMethod.Get, path, null, null, cancellationToken);
        }

        public Task<IServiceResult<TResult>> GetAsync<TResult>(string path, CancellationToken? cancellationToken = null)
        {
            var executeResult = ExecuteAsync<string, TResult>(HttpMethod.Get, path, null, null, cancellationToken);
            return executeResult;
        }

        public Task<IServiceResult<TResult>> GetAsync<TRequest, TResult>(string path, TRequest data, CancellationToken? cancellationToken = null)
        {
            return ExecuteAsync<TRequest, TResult>(HttpMethod.Get, path, data, null, cancellationToken);
        }

        public Task<object> GetAsync(SettingKeys settingKey, string path, object data, CancellationToken cancellationToken = default)
        {
            var resourceType = settingKey.GetResourceType();
            var task = this.GetType()
                    .GetMethod(nameof(ExecuteAsync))?
                    .MakeGenericMethod(typeof(object), settingKey.GetResourceType())
                    .Invoke(this, [HttpMethod.Get, path, null, null, cancellationToken]);
            //var type = typeof(IOptimaResult<>).MakeGenericType(resourceType);
            var taskType = typeof(Task<>).MakeGenericType(typeof(IOptimaResult<>).MakeGenericType(resourceType));

            var result = Convert.ChangeType(task, taskType) as Task<object>;
            return result;
        }

        /// <summary> PATCH ASYNC request to api with SaveChanges, path is relative or root</summary>
        public Task<IServiceResult<TResult>> UpdateAsync<TRequest, TResult>(string path, TRequest data, CancellationToken? cancellationToken = null)
        {
            return ExecuteAsync<TRequest, TResult>(HttpMethod.Patch, path, data, null, cancellationToken);
        }

        /// <summary> PUT ASYNC request to api with SaveChanges, path is relative or root</summary>
        public Task<IServiceResult<TResult>> PutAsync<TRequest, TResult>(string path, TRequest data, CancellationToken? cancellationToken = null)
        {
            return ExecuteAsync<TRequest, TResult>(HttpMethod.Put, path, data, null, cancellationToken);
        }

        /// <summary> DELETE request to api with SaveChanges, path is relative or root</summary>
        public Task<IServiceResult<TResult>> Delete<TRequest, TResult>(string path, TRequest data, CancellationToken? cancellationToken = null)
        {
            return ExecuteAsync<TRequest, TResult>(HttpMethod.Delete, path, data, null, cancellationToken);
        }

        /// <summary> DELETE ASYNC request to api with SaveChanges, path is relative or root</summary>
        public Task<IServiceResult<TResult>> DeleteAsync<TRequest, TResult>(string path, TRequest data, CancellationToken? cancellationToken = null)
        {
            return ExecuteAsync<TRequest, TResult>(HttpMethod.Delete, path, data, null, cancellationToken);
        }

        /// <summary> ASYNC POST request to api, path is relative or root</summary>
        public Task<IServiceResult<TResult>> PostAsync<TRequest, TResult>(string path, TRequest data, CancellationToken? cancellationToken = null)
        {
            return ExecuteAsync<TRequest, TResult>(HttpMethod.Post, path, data, null, cancellationToken);
        }

        /// <summary>
        /// SendDataListAsync, it will execute dynamic http request with parameters: httpMethod, path, dynamic payload request
        /// </summary>
        public Task<IServiceResult<TResult>> SendAsync<TRequest, TResult>(HttpMethod httpMethod, string path, TRequest data, CancellationToken? cancellationToken = null)
        {
            return ExecuteAsync<TRequest, TResult>(httpMethod, path, data, null, cancellationToken);
        }

        #endregion public methods

        #region private methods

        private async Task<IServiceResult<TResult>> ExecuteAsync<TRequest, TResult>(
            HttpMethod method, string path, TRequest? request = default,
            IEnumerable<KeyValuePair<string, IEnumerable<string>>>? headerParams = null,
            CancellationToken? cancellationToken = default)
        {
            IServiceResult<TResult> serviceResult;
            try
            {
                // create http request message
                cancellationToken ??= CancellationToken.None;
                var requestUri = new Uri(path, UriKind.Relative);
                var requestMessage = new HttpRequestMessage(method, requestUri);

                // http method Get
                if (method == HttpMethod.Get)
                {
                    requestUri = request != null
                        ? new Uri($"{path}?{ToQueryString(request)}", UriKind.Relative)
                        : new Uri($"{path}", UriKind.Relative);
                    requestMessage.RequestUri = requestUri;
                }
                // http method Post and others
                else
                {
                    requestMessage.Content = MakeRequestContent(method, request);
                }

                // add correlationId
                var httpItems = _httpContextAccessor.HttpContext?.Items;
                var correlationId = httpItems != null && httpItems.TryGetValue(HttpContextItemsKeys.CorrelationId, out var existingCorrelationId)
                    ? existingCorrelationId?.ToString() ?? Guid.NewGuid().ToString()
                    : Guid.NewGuid().ToString();

                // add authorization
                if (_httpClient.DefaultRequestHeaders.Contains(RequestHeaderKeys.Authorization))
                    requestMessage.Headers.Add(RequestHeaderKeys.Authorization, _httpClient.DefaultRequestHeaders.Authorization?.ToString());

                // add options
                requestMessage.Options.Set(new HttpRequestOptionsKey<string>(RequestHeaderKeys.HttpClientName), ClientName);
                requestMessage.Options.Set(new HttpRequestOptionsKey<string>(RequestHeaderKeys.CorrelationId), correlationId);
                requestMessage.Options.Set(new HttpRequestOptionsKey<Type>("TypeOfResult"), typeof(TResult));

                // add headers
                requestMessage.Headers.Add(RequestHeaderKeys.CorrelationId, correlationId);
                requestMessage.Headers.Add(RequestHeaderKeys.HttpClientName, ClientName);
                foreach (var (key, value) in headerParams ?? [])
                {
                    requestMessage.Headers.Add(key, value);
                }

                // send request
                var response = await _httpClient.SendAsync(requestMessage, (CancellationToken)cancellationToken);
                var requestUrl = response.RequestMessage?.RequestUri?.ToString() ?? "";

                // handle response
                string requestData = string.Empty;
                bool isRequestValid = false;
                if (response.RequestMessage?.Content != null)
                {
                    var (contentRequestData, contentIsRequestValid) = await GetContentDataAsync<TResult>(response.RequestMessage?.Content);
                    requestData = contentRequestData ?? "";
                    isRequestValid = contentIsRequestValid;
                }
                else
                {
                    requestData = requestUrl;
                    isRequestValid = string.IsNullOrEmpty(requestData);
                }

                var (responseString, isRsponseValid) = await GetContentDataAsync<TResult>(response.Content, response.StatusCode);

                // make service result
                if (response.IsSuccessStatusCode && isRsponseValid)
                {
                    var result = typeof(TResult) != typeof(string)
                        ? JsonConvert.DeserializeObject<TResult>(responseString ?? "null")
                        : (TResult)(object)(responseString ?? string.Empty);

                    serviceResult = ServiceResult<TResult>.Ok(result);
                }
                else if (response.IsSuccessStatusCode && !isRsponseValid)
                {
                    serviceResult = ServiceResult<TResult>.PartialContent(responseString);
                }
                else
                {
                    var errMsg = $"HttpClient {ClientName} failed. StatusCode: {response.StatusCode}. Message: {response.ReasonPhrase}";
                    _logger.Error("HttpClient {CLIENTNAME} failed. Url: {URL}. Message: {MESSAGE}",
                        ClientName, $"{BaseAddress}{path}", errMsg);
                    serviceResult = ServiceResult<TResult>.Error(errMsg, (int)response.StatusCode);
                }

                serviceResult.RequestData = requestData ?? "";
                serviceResult.RequestUrl = requestUrl;
            }
            catch (ApiException ex)
            {
                _logger.Error("HttpClient {ClientName} failed. Message: {Message}. \nRequestUrl: {requestUrl}. \nResponse: {Response}. \nStackTrace {StackTrace}\n",
                    ClientName, ex?.Message, ex?.RequestUrl, ex?.ResponseData, ex?.StackTrace);
                serviceResult = ServiceResult<TResult>.Error(ex);
            }
            catch (Exception ex)
            {
                _logger.Error("HttpClient {ClientName} failed. Message: {Message}. \nRequestUrl: {requestUrl}. \nStackTrace {StackTrace}\n",
                    ClientName, ex?.Message, $"{BaseAddress}{path}", ex?.StackTrace);
                serviceResult = ServiceResult<TResult>.Error(ex);
            }

            return serviceResult;
        }

        /// <summary> Make request content for httpClient </summary>
        private ByteArrayContent MakeRequestContent<TRequest>(HttpMethod method, TRequest? request)
        {
            ByteArrayContent requestContent;
            string requestJsonString;

            if (method.Method == HttpMethod.Get.Method)
            {
                requestJsonString = JsonConvert.SerializeObject(request != null ? request : new { });
                requestContent = new StringContent(requestJsonString, Encoding.UTF8, ContentType);
            }
            else
            {
                requestJsonString = JsonConvert.SerializeObject(request);
                requestContent = new ByteArrayContent(Encoding.UTF8.GetBytes(requestJsonString));
                //requestContent = new StringContent(requestJsonString, Encoding.UTF8, ContentType);
            }

            requestContent.Headers.ContentType = new MediaTypeHeaderValue(ContentType);
            return requestContent;
        }

        /// <summary> Make uri from string </summary>
        private Uri MakeUri(string? uri) => new(uri ?? "", UriKind.RelativeOrAbsolute);

        public static async Task<(string?, bool)> GetContentDataAsync_(HttpContent? content, HttpStatusCode? httpStatusCode = null)
        {
            if (content == null)
                return (null, false);

            using Stream responseStream = await content.ReadAsStreamAsync();
            using StreamReader reader = new(responseStream, Encoding.UTF8);
            string responseContent = await reader.ReadToEndAsync();
            return (responseContent, true);
        }

        /// <summary>
        /// GetContentDataAsync - parse httpContent
        /// </summary>
        private async Task<(string?, bool)> GetContentDataAsync<TResult>(
            HttpContent? content, HttpStatusCode? httpStatusCode = null)
        {
            if (content == null)
                return (null, false);

            var responseContent = await content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseContent))
                return ("", false);

            var contentType = content.Headers.ContentType?.MediaType ?? "";
            bool isValidContent = true;

            if (contentType == MediaTypeNames.Application.Json)
            {
                isValidContent = ApiJsonHelper.IsValidAndNotEmpty<TResult>(responseContent);
            }
            else if (contentType == MediaTypeNames.Text.Html || contentType == MediaTypeNames.Text.Xml)
            {
                isValidContent = ApiHelper.IsHtmlValid(responseContent);

                if (httpStatusCode == HttpStatusCode.Forbidden)
                {
                    responseContent = GetHtmlNodeText(responseContent, "//head//title");
                }
            }

            return (responseContent, isValidContent);
        }

        /// <summary>
        /// Selects the first XmlNode that matches the XPath expression.
        /// </summary>
        private string GetHtmlNodeText(string htmlString, string xPath)
        {
            try
            {
                XmlDocument xmlDoc = new();
                xmlDoc.LoadXml(htmlString);
                XmlNode? headNode = xmlDoc.SelectSingleNode(xPath);
                return headNode != null ? headNode.InnerText : string.Empty;
            }
            catch (XmlException)
            {
                return string.Empty;
            }
        }

        private string ToQueryString(object requestData)
        {
            var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);

            foreach (var prop in requestData.GetType().GetProperties())
            {
                var value = prop.GetValue(requestData);
                if (value != null)
                    queryString[prop.Name] = value.ToString();
            }
            return queryString?.ToString() ?? "";
        }

        private string ToQueryString2(object requestData)
        {
            var properties = requestData.GetType().GetProperties();
            var keyValuePairs = new List<string>();

            foreach (var property in properties)
            {
                var value = property.GetValue(requestData);
                if (value != null)
                {
                    if (property.PropertyType.IsArray || property.PropertyType.IsGenericType)
                    {
                        var enumerable = (System.Collections.IEnumerable)value;
                        foreach (var item in enumerable)
                        {
                            keyValuePairs.Add($"{property.Name}={item}");
                        }
                    }
                    else
                    {
                        keyValuePairs.Add($"{property.Name}={value}");
                    }
                }
            }

            return string.Join("&", keyValuePairs);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        #endregion private methods
    }
}
