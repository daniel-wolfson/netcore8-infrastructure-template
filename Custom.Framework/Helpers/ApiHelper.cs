using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Exceptions;
using Custom.Framework.Models.Base;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Serilog;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Custom.Framework.Helpers
{

    public static class ApiHelper
    {
        /// <summary>
        /// Default service name if service name is not provided.
        /// </summary>
        public static readonly string ServiceName = Assembly.GetEntryAssembly()?.GetName().Name ?? "";

        //$"{System.Diagnostics.Process.GetCurrentProcess().ProcessName}";
        public static readonly string ServiceVersion = Assembly.GetEntryAssembly()?.GetName().Version!.ToString() ?? "";

        public static string GetApiMessageInfo(this object error, string message = "", Type? operationType = null)
        {
            var errMsg = "error";

            if (error.GetType().BaseType == typeof(Exception) || error.GetType().BaseType == typeof(SystemException))
            {
                errMsg = ((Exception)error).InnerException?.Message ?? ((Exception)error).Message ?? errMsg;
            }
            else if (error.GetType() == typeof(string))
            {
                errMsg = error.ToString();
            }

            return $"errorHeader: {Assembly.GetEntryAssembly()?.GetName().Name ?? ""}: {message}" +
                   $"errorMessage: {errMsg}; " +
                   $"errorType: {error.GetType().Name}; " +
                   $"{"errorOperationType:" + operationType?.GetType().Name ?? ""}";
        }

        public static string GetDisplayName(this Type type, string propertyName)
        {
            PropertyInfo? propertyInfo = type.GetProperty(propertyName);

            if (propertyInfo == null) return "";

            var displayAttribute = propertyInfo?.GetCustomAttribute<DisplayAttribute>();

            if (displayAttribute != null)
            {
                return displayAttribute.Name ?? "";
            }

            return propertyInfo?.Name ?? "";
        }

        public static T DeepCopy<T>(this T self)
        {
            var serialized = JsonConvert.SerializeObject(self);
            return JsonConvert.DeserializeObject<T>(serialized)!;
        }

        /// <summary>
        /// IsEnumerable returns that object implement IEnumerable
        /// </summary>
        public static bool IsEnumerable(this Type type)
        {
            return type.GetInterfaces()
                .Any(interfaceType => interfaceType == typeof(IEnumerable<>)
                    || interfaceType.IsGenericType
                        && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        }

        /// <summary> 
        /// IsCollection returns that object is collection
        /// </summary>
        public static bool IsCollection(this Type type)
        {
            return typeof(IEnumerable<>).IsAssignableFrom(type) && !type.IsArray && type != typeof(string);
        }

        public static IEnumerable<T> ToEnums<T>(this IEnumerable<string> enumValues)
        where T : Enum
        {
            var validEnums = enumValues
                .Where(k => Enum.IsDefined(typeof(T), k))
                .Select(k => Enum.Parse(typeof(T), k))
                .Cast<T>();

            return validEnums!;
        }

        /// <summary> Is Valid Html </summary>
        public static bool IsHtmlValid(string input)
        {
            // Regular expression to check if the string contains HTML tags
            string htmlPattern = @"<[^>]+>";

            // Check if the input string contains any HTML tags
            return Regex.IsMatch(input, htmlPattern);
        }

        public static string RemoveHtmlTags(string? input, string pattern = "<.*?>")
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;
            return Regex.Replace(input, pattern, string.Empty);
        }

        /// <summary> 
        /// LogTitle, get current filePath and memberName throught caller attributes
        /// Warning!!! It's not recommended to use this method in loop code
        /// </summary>
        public static string LogTitle(string? title = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            var filePath = callerFilePath.Replace("\\", "/");
            var callerTypeName = Path.GetFileNameWithoutExtension(filePath);
            title = string.IsNullOrEmpty(title) ? string.Empty : " " + title;
            return $"{callerTypeName}.{callerMemberName}{title}:";
        }

        /// <summary> Use regular expressions to replace symbols with an empty string </summary>
        public static string? CleanSymbols(string? input, string symbols = @"\r\n\t ")
        {
            if (input == null) return default;

            string result = string.Empty;
            var includeSpaces = symbols.IndexOf(" ") > 0;
            if (includeSpaces)
            {
                Regex regexRemoveSpaces = new(@$"[ ]+");
                symbols = regexRemoveSpaces.Replace(symbols, string.Empty);
            }

            if (!string.IsNullOrEmpty(symbols))
            {
                Regex regexRemoveSpaces = new(@$"[{symbols}]+");
                result = regexRemoveSpaces.Replace(input, string.Empty);
            }

            if (includeSpaces)
            {
                Regex regexReplaceSpaces = new(@$"[ ]+");
                result = regexReplaceSpaces.Replace(result, " ");
            }

            return result;
        }

        /// <summary> Verify Is data not null or empty </summary>
        public static bool IsDataNullOrEmpty(object? data, Type? resourceType = null)
        {
            if (data == null)
                return true;

            return data switch
            {
                string str => string.IsNullOrEmpty(str),
                ICollection collection => collection.Count == 0,
                IEnumerable enumerable => !enumerable.Cast<object>().Any(),
                _ => resourceType != null
                    && resourceType.GetDefault()?.ToString() == JsonConvert.SerializeObject(data)
            };
        }

        public static List<T> IntersectResults<T>(List<T> settings1, List<T> settings2)
            where T : EntityData
        {
            return settings1.IntersectBy(settings2
                    .Where(x => ApiHelper.IsDataNullOrEmpty(x.Value)).Select(x => x.SettingKey), x => x.SettingKey)
                    .ToList();
        }

        public static string RemoveSlashes(string blobName)
        {
            string blobNameResult = blobName;

            // Remove slash
            while (string.IsNullOrWhiteSpace(blobNameResult) == false && blobNameResult.StartsWith(@"/"))
            {
                blobNameResult = blobNameResult.Substring(1);
            }

            // Remove backslash
            while (string.IsNullOrWhiteSpace(blobNameResult) == false && blobNameResult.StartsWith(@"\"))
            {
                blobNameResult = blobNameResult.Substring(1);
            }

            return blobNameResult;
        }

        /// <summary>
        /// GetDefault is return default typed object of type 
        /// </summary>
        public static T GetDefault<T>(this Type type)
        {
            try
            {
                if (typeof(T).IsClass || typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null)
                {
                    if (typeof(T) == typeof(string))
                    {
                        return (T)(object)string.Empty;
                    }

                    if (typeof(T) == typeof(object))
                    {
                        return (T?)Activator.CreateInstance(type)
                            ?? throw new ApiException(new InvalidOperationException($"{ApiHelper.LogTitle()} failed"));
                    }

                    if (typeof(T).IsClass)
                    {
                        return (T?)Activator.CreateInstance(typeof(T))
                            ?? throw new ApiException(new InvalidOperationException($"{ApiHelper.LogTitle()} failed"));
                    }
                }
                return default!;
            }
            catch (Exception ex)
            {
                throw new ApiException($"{ApiHelper.LogTitle()} failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        /// <summary>
        /// GetDefault returns default json of type 
        /// </summary>
        public static object GetDefault(this Type type)
        {
            try
            {
                if (type.IsClass || type.IsValueType && Nullable.GetUnderlyingType(type) == null)
                {
                    if (type == typeof(string))
                    {
                        return string.Empty;
                    }
                    else if (type.IsClass & type == typeof(string[]))
                    {
                        return Activator.CreateInstance(typeof(List<string>), [0])!;
                    }
                    else if (type.IsClass & type == typeof(int[]))
                    {
                        return Activator.CreateInstance(typeof(List<int>), [0])!;
                    }
                    else if (type.IsClass)
                    {
                        return Activator.CreateInstance(type)!;
                    }
                }
                return default!;
            }
            catch (Exception ex)
            {
                throw new ApiException($"{ApiHelper.LogTitle()} failed: {ex.InnerException?.Message ?? ex.Message}. StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// GetDefault returns default object of type by SettingKeys
        /// </summary>
        public static T GetDefault<T>(this SettingKeys settingKey)
        {
            return settingKey.GetResourceType().GetDefault<T>();
        }

        /// <summary>
        /// GetDefault returns default object of type by SettingKeys
        /// </summary>
        public static object GetDefault(this SettingKeys settingKey)
        {
            return settingKey.GetResourceType().GetDefault();
        }

        public static bool IsMobileDevice(HttpRequest request)
        {
            try
            {
                var userAgent = request.Headers["User-Agent"].ToString();
                if (userAgent.Contains("Mobi", StringComparison.OrdinalIgnoreCase) ||
                    userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                    userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
                    userAgent.Contains("Windows Phone", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool GetHttpHeader(this HttpRequest request, string headerName)
        {
            try
            {
                if (request.Headers.TryGetValue(headerName, out var headerValue))
                {
                    if (bool.TryParse(headerValue, out var useRedis))
                    {
                        return useRedis;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Error("{TITLE} error: {MESSAGE}", ApiHelper.LogTitle(), ex?.InnerException?.Message ?? ex?.Message);
                return false;
            }
        }

        public static string GetMethodNameFromStackTrace(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
            {
                return string.Empty;
            }

            // Split the stack trace into lines
            string[] lines = stackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            if (lines.Length == 0)
            {
                return string.Empty;
            }

            // Assuming the first line contains the method name
            string firstLine = lines[0];

            // Custom format of stack trace line: "at Namespace.Class.Method() in File:line X"
            // Find the position of "at " and "("
            int atIndex = firstLine.IndexOf("at ");
            int parenIndex = firstLine.IndexOf('(');

            if (atIndex == -1 || parenIndex == -1)
            {
                return string.Empty;
            }

            // Extract the method name
            string methodName = firstLine.Substring(atIndex + 3, parenIndex - atIndex - 3).Trim();

            return methodName;
        }

        public static bool IsDigitsOnly(string? str)
        {
            if (string.IsNullOrEmpty(str))
                return false;
            return Regex.IsMatch(str, @"^\d+$");
        }

        public static bool IsDigitsOnly(object? strObj)
        {
            var str = strObj?.ToString();
            if (string.IsNullOrEmpty(str))
                return false;
            return Regex.IsMatch(str, @"^\d+$");
        }

        public static void AddOrUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
            where TKey : notnull
        {
            dictionary[key] = value; // Uses indexer - overwrites if exists
        }
    }
}