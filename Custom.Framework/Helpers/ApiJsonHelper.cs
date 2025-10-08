using Confluent.Kafka;
using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Exceptions;
using Custom.Framework.Models;
using Custom.Framework.Models.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Custom.Framework.Helpers
{
    public static class ApiJsonHelper
    {
        private static JsonSerializerOptions _defaultJsonOptions;
        public static JsonSerializerOptions DefaultJsonOptions
        {
            get
            {
                if (_defaultJsonOptions == null)
                {
                    _defaultJsonOptions = new JsonSerializerOptions
                    {
                        WriteIndented = false,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        //PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };
                }
                return _defaultJsonOptions;
            }
        }

        public static object? GetValueOrDefault(this Stream stream, Type resourceType)
        {
            object? result = stream.GetValue(resourceType);
            result ??= resourceType.GetDefault();
            return result;
        }

        public static TValue? GetValueOrDefault<TValue>(this Stream stream)
        {
            var result = GetValue<TValue>(stream);
            result ??= typeof(TValue).GetDefault<TValue>();
            return result;
        }

        public static object? GetValueOrDefault(this string value, Type resourceType)
        {
            var result = ApiConvertHelper.ChangeType(value, resourceType);
            result ??= resourceType.GetDefault();
            return result;
        }

        public static object? GetValueOrDefault<TData>(this OptimaResult<List<TData>> value, Type resourceType)
        {
            var result = ApiConvertHelper.ChangeType(value, resourceType);
            result ??= resourceType.GetDefault();
            return result;
        }

        public static object? GetValue(this Stream stream, Type resourceType)
        {
            object? result = default;

            try
            {
                using var reader = new StreamReader(stream);
                string content = reader.ReadToEnd();

                //if (resourceType == typeof(List<UmbracoSettings>))
                //    content = ApiConvertHelper.ToUmbracoList(content);

                if (resourceType == typeof(string))
                {
                    var @object = JsonConvert.DeserializeObject(content, resourceType);
                    result = Convert.ChangeType(@object?.ToString(), resourceType);
                }
                else
                {
                    result = JsonConvert.DeserializeObject(content, resourceType);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                        ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                result = resourceType.GetDefault();
            }

            return result;
        }

        public static TValue? GetValue<TValue>(this Stream stream, string? key = null)
        {
            var isSettingKey = Enum.TryParse<SettingKeys>(key, out SettingKeys settingKey);
            object? result = stream.GetValue(isSettingKey ? settingKey.GetResourceType() : typeof(TValue));
            return (TValue?)result;
        }

        /// <summary>
        /// GetDefault is return default Json string by type 
        /// </summary>
        public static string GetJsonDefault(this Type type)
        {
            if (type == typeof(bool))
            {
                return JsonConvert.SerializeObject(false);
            }
            else if (type == typeof(int))
            {
                return JsonConvert.SerializeObject(0);
            }
            else if (type == typeof(string))
            {
                return JsonConvert.SerializeObject(string.Empty);
            }
            else if (type.IsEnumerable())
            {
                return JsonConvert.SerializeObject(Array.Empty<int>());
            }
            else
                return JsonConvert.SerializeObject(new object());
        }

        public static string ToJson(this HealthReport report, string checkName)
        {
            string json = JsonConvert.SerializeObject(
                new
                {
                    Name = checkName,
                    Status = report.Status.ToString(),
                    Duration = report.TotalDuration,
                    Info = report.Entries
                        .Select(e =>
                            new
                            {
                                Key = e.Key,
                                Description = e.Value.Description,
                                Duration = e.Value.Duration,
                                Status = Enum.GetName(typeof(HealthStatus), e.Value.Status),
                                Error = e.Value.Exception?.Message,
                                Data = e.Value.Data
                            })
                        .ToList()
                });

            return json;
        }

        public static string ToJson<TKey, TValue>(this ConsumeResult<TKey, TValue> result)
        {
            if (result == null)
                return string.Empty;

            try
            {
                var jsonString = JsonConvert.SerializeObject(new
                {
                    result.Topic,
                    Partition = result.Partition.Value,
                    Offset = result.Offset.Value,
                    result.Message.Key,
                    result.Message.Value,
                    Timestamp = result.Message.Timestamp.UtcDateTime
                        .ToString("o", CultureInfo.InvariantCulture),
                    Headers = result.Message.Headers?
                        .ToDictionary(h => h.Key, h => h.GetValueBytes() != null 
                            ? System.Text.Encoding.UTF8.GetString(h.GetValueBytes()) : null)
                });
                return jsonString;
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                return string.Empty;
            }
        }

        /// <summary>
        /// GetObject from fileJson
        /// </summary>
        public static T? GetObjectFromFileJson<T>(string fileName)
        {
            var fileExt = string.IsNullOrEmpty(Path.GetExtension(fileName)) ? ".json" : "";
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "data", fileName + fileExt);

            if (!File.Exists(filePath))
            {
                Log.Logger.Error("{TITLE} error: json data file {filePath} not found.", ApiHelper.LogTitle(), filePath);
                return default;
            }

            try
            {
                var data = File.ReadAllText(filePath);
                var result = JsonConvert.DeserializeObject<T>(data)
                    ?? throw new ApiException(ServiceStatus.FatalError, $"{ApiHelper.LogTitle()}: fileName {filePath} have't deserialized and returns null");

                return result;
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} error: json data file {FILENAME} failed. Exception: {EXCEPTION}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), filePath, ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                return default;
            }
        }

        /// <summary>
        /// GetObject async from fileJson
        /// </summary>
        public static async Task<T?> GetObjectFromFileJsonAsync<T>(string fileName)
        {
            var fileExt = string.IsNullOrEmpty(Path.GetExtension(fileName)) ? ".json" : "";
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "data", fileName + fileExt);

            if (!File.Exists(filePath))
                throw new ApiException(ServiceStatus.FatalError, $"{ApiHelper.LogTitle()} error: json data file {filePath} not found.");

            try
            {
                var data = await File.ReadAllTextAsync(filePath);
                var result = JsonConvert.DeserializeObject<T>(data)
                    ?? throw new ApiException(ServiceStatus.FatalError, $"{ApiHelper.LogTitle()}: fileName {filePath} have't deserialized and returns null");

                return result;
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} error: json data file {FILENAME} failed. Exception: {EXCEPTION}. \nStackTrace: {STACKTRACE}\n",
                    ApiHelper.LogTitle(), filePath, ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                return default;
            }
        }

        /// <summary>
        /// SaveObjectToFileJson
        /// </summary>
        public static bool SaveObjectToFileJson<T>(string fileName, T obj)
        {
            var fileExt = string.IsNullOrEmpty(Path.GetExtension(fileName)) ? ".json" : "";
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "data", fileName + fileExt);

            var json = JsonConvert.SerializeObject(obj);
            File.WriteAllText(filePath, json);

            var result = File.Exists(filePath);
            if (!result)
                Debug.WriteLine($"{nameof(SaveObjectToFileJson)} error: json data file {filePath} not found.");

            return result;
        }

        public static (bool IsValid, string Message) Validate(SettingKeys settingKey, string? jsonCurrent, string? jsonUpdated)
        {
            var jsonEmpty = settingKey.GetJsonDefault();
            var message = string.Empty;
            var isValid = true;

            if (string.IsNullOrEmpty(jsonUpdated))
            {
                message = $"data is empty or null, about this the json not may be updated";
                isValid = false;
            }
            else if (jsonUpdated == jsonEmpty)
            {
                if (jsonCurrent == jsonEmpty)
                    message = $"data is EMPTY and NO LAST GOOD configuration, configuration not may be updated";
                else
                    message = $"data is EMPTY, configuration will USE LAST GOOD configuration";
                isValid = false;
            }
            else if (jsonUpdated == jsonCurrent)
            {
                message = $"data equals to last result, configuration will not updated";
                isValid = true;
            }
            return (isValid, message);
        }

        public static (bool IsValid, string Message) Validate<T>(string? json)
        {
            var message = string.Empty;
            var isValid = true;
            var jsonDefault = typeof(T).GetJsonDefault();

            if (string.IsNullOrEmpty(json))
            {
                message = $"data is empty or null, about this the json not may be updated";
                isValid = false;
            }
            else if (json == jsonDefault)
            {
                message = $"data is EMPTY, configuration will USE LAST GOOD configuration";
                isValid = false;
            }
            else if (IsNullOrEmpty(json))
            {
                message = $"data is empty or null, about this the json not may be updated";
                isValid = false;
            }

            return (isValid, message);
        }

        public static (bool IsValid, string Message) Validate(string? jsonEmpty, string? jsonCurrent, string? jsonUpdated)
        {
            var message = string.Empty;
            var isValid = true;

            if (string.IsNullOrEmpty(jsonUpdated))
            {
                message = $"data is empty or null, about this the json not may be updated";
                isValid = false;
            }
            else if (jsonUpdated == jsonEmpty)
            {
                if (jsonCurrent == jsonEmpty)
                    message = $"data is EMPTY and NO LAST GOOD configuration, configuration not may be updated";
                else
                    message = $"data is EMPTY, configuration will USE LAST GOOD configuration";
                isValid = false;
            }
            else if (jsonUpdated == jsonCurrent)
            {
                message = $"data equals to last result, configuration will not updated";
                isValid = false;
            }
            return (isValid, message);
        }

        public static bool IsNullOrEmpty(string? jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
                return true;

            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonString);
                JsonElement root = doc.RootElement;
                return (root.ValueKind == JsonValueKind.Object && !root.EnumerateObject().Any()
                    || (root.ValueKind == JsonValueKind.Array && !root.EnumerateArray().Any()));
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                Log.Logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                return true;
            }
        }


        public static bool IsJsonString(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            var trimmed = json.Trim();
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                return doc.RootElement.ValueKind == JsonValueKind.String;
            }
            catch (System.Text.Json.JsonException)
            {
                return false;
            }
        }

        public static bool ContainsValidJson(string text, out string? jsonPart)
        {
            jsonPart = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Ищем первую '{' и последнюю '}'
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');

            if (start == -1 || end == -1 || end <= start)
                return false;

            jsonPart = text.Substring(start, end - start + 1);

            // Проверяем сбалансированность скобок
            int depth = 0;
            foreach (char c in jsonPart)
            {
                if (c == '{') depth++;
                else if (c == '}') depth--;

                if (depth < 0) // лишняя закрывающая скобка
                    return false;
            }

            if (depth != 0)
                return false; // есть незакрытые скобки

            // Теперь пробуем разобрать JSON
            try
            {
                using var doc = JsonDocument.Parse(jsonPart);
                return true;
            }
            catch (System.Text.Json.JsonException)
            {
                return false;
            }
        }

        public static bool IsValidAndNotEmpty<T>(string jsonString)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jsonString))
                    return false;
                if (typeof(T) == typeof(int))
                    return int.TryParse(jsonString, out var value);
                if (typeof(T) == typeof(string))
                    return IsJsonString(jsonString);

                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;
                return root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Any()
                    || root.ValueKind == JsonValueKind.Array && root.EnumerateArray().Any();
            }
            catch (System.Text.Json.JsonException ex)
            {
                Log.Error("{TITLE} exception: {EXCEPTION}. \nStackTrace: {STACKTRACE}\n",
                          ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                return false;
            }
        }
    }
}