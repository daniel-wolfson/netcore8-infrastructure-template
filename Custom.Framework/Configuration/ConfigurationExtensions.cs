using Custom.Framework.Configuration.Models;
using Custom.Framework.Configuration.Umbraco;
using Custom.Framework.Exceptions;
using Custom.Framework.Extensions;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Custom.Framework.Models.Base;
using Custom.Framework.Telemetry;
using Microsoft.Extensions.Configuration;
using Pipelines.Sockets.Unofficial.Arenas;
using Serilog;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Custom.Framework.Configuration
{
    public static class ConfigurationExtensions
    {
        public static IEnumerable<ConfigData> GetSections(this IConfiguration config, SettingKeys sectionKey)
        {
            return config.GetSections(sectionKey.ToString());
        }

        public static IEnumerable<ConfigData> GetSections(this IConfiguration config, string sectionName)
        {
            // get app sections by sectionName
            var sections = config.GetSection(sectionName)
                .Get<IEnumerable<ConfigData>>() ?? [];

            // set settingKey enum and order if not defined
            sections = sections.Select(item =>
            {
                var isSettingKey = Enum.TryParse<SettingKeys>(item.Name, out SettingKeys settingKey);
                if (isSettingKey)
                {
                    // set settingKey if not defined
                    if (item.SettingKey == SettingKeys.Unknown)
                        item.SettingKey = settingKey;
                }

                // set order if not defined
                item.Order = item.Order == 0 ? int.MaxValue : item.Order;
                return item;
            })
            .OrderBy(x => x.Order)
            .ToList();

            return sections ?? [];
        }

        /// <summary> 
        /// Calculate list values from data property
        /// </summary>
        public static List<TData> ToDataList<TData>(this IEnumerable<TData> settings)
            where TData : EntityData
        {
            foreach (var item in settings.Where(x => x.Provider == ProviderTypes.StaticList && x.Value == null))
            {
                if (item.Data is not string data) continue;

                var dataItems = data.Split(',');
                item.Value = dataItems.All(ApiHelper.IsDigitsOnly)
                    ? dataItems.Select(int.Parse).ToList()
                    : dataItems.ToList();
            }
            return settings.ToList();
        }

        /// <summary>
        /// ToEntityList - Calculate list values from data property
        /// </summary>
        public static List<EntityData> ToEntityList(this IEnumerable<Result> settings)
        {
            return settings.Select(item =>
            {
                Enum.TryParse(item.Name, out SettingKeys settingKey);
                var result = new EntityData()
                {
                    SettingKey = settingKey,
                    Name = item.Name,
                    Error = item.Error,
                    Message = item.Message,
                };
                return result;
            }).ToList();
        }

        //public static List<EntityData> ToEntityList(this IEnumerable<DataResult> settings)
        //{
        //    return settings.Select(item =>
        //    {
        //        Enum.TryParse(item.Name, out SettingKeys settingKey);

        //        var result = new EntityData()
        //        {
        //            SettingKey = settingKey,
        //            Name = item.Name,
        //            Error = item.Error,
        //            Message = item.Message,
        //            Data = item.Data,
        //            Value = item.Value
        //        };

        //        //var dataItems = item.Data?.ToString()?.Split(',') ?? Array.Empty<string>();
        //        //item.Value = dataItems.All(ApiHelper.IsDigitsOnly)
        //        //    ? dataItems.Select(int.Parse).ToList()
        //        //    : dataItems.ToList();

        //        return result;
        //    }).ToList();
        //}

        //public static List<TData> ToDataList<TData>(this string data)
        //{
        //    var dataItems = data?.ToString()?.Split(",").ToList() ?? [];
        //    if (dataItems.Count > 0 && dataItems.All(ApiHelper.IsDigitsOnly))
        //        return dataItems.Select(int.Parse)?.Cast<TData>()?.ToList() ?? [];
        //    else
        //        return dataItems.Cast<TData>().ToList();
        //}

        //public static List<T> ToDataList<T>(this EntityData settings)
        //{
        //    var sets = settings.Data?.ToString()?.Split(",")
        //        .ToList()
        //        .Select(x => ApiHelper.IsDigitsOnly(x) ? (object)int.Parse(x) : x)
        //        .OfType<T>()
        //        .ToList();
        //    return sets;
        //}

        /// <summary> 
        /// Get section data by SettingKeys 
        /// </summary>
        public static ConfigData? GetSection(this IConfiguration configuration,
            SettingKeys sectionSettingKey, SettingKeys itemSettingKey)
        {
            var value = configuration
                .GetSections(sectionSettingKey.ToString())
                .FirstOrDefault(x => x.SettingKey == itemSettingKey);

            return value;
        }

        /// <summary>
        /// Get section data by SettingKeys
        /// </summary>
        public static ConfigData? GetSection(this IConfiguration configuration, SettingKeys sectionSettingKey)
        {
            var value = configuration
                .GetSections(SettingKeys.StaticData.ToString())
                .FirstOrDefault(x => x.SettingKey == sectionSettingKey);
            return value;
        }

        /// <summary> 
        /// Get sections by settingKey 
        /// </summary>
        public static EntityData? GetSection(this IConfiguration configuration, string key)
        {
            try
            {
                SettingKeys settingKey = SettingKeys.Unknown;
                key = key.Contains(':') ? key.Split(":").Last() : $"{SettingKeys.StaticData}:{key}";

                var isValidSettingKey = Enum.TryParse(key, out settingKey);

                var opt = configuration.GetSection(key).Get<EntityData>();
                if (opt != null && isValidSettingKey)
                    opt.SettingKey = settingKey;

                return opt;
            }
            catch (Exception ex)
            {
                throw new ApiException(ServiceStatus.FatalError, ex);
            }
        }

        /// <summary> 
        /// Get all Umbraco sections from config 
        /// </summary>
        public static UmbracoSettings ConvertToUmbracoSettings(this List<EntityData> dataSource,
            int rootNodeId, string? hotelCode = null)
        {
            var allUmbracoSettings = dataSource
                .GetValueOrDefault<List<UmbracoSettings>>(SettingKeys.UmbracoSettings);

            var umbracoSettingsData = allUmbracoSettings?.FirstOrDefault(x => x.Id == rootNodeId);
            var umbracoSettings = umbracoSettingsData.DeepCopy();

            if (hotelCode != null && umbracoSettings != null)
            {
                umbracoSettings.ConnectingDoorSettings = umbracoSettings.ConnectingDoorSettings.Where(x => x.HotelCode == hotelCode).ToList();
                umbracoSettings.RoomsFilter = umbracoSettings.RoomsFilter.Where(x => x.HotelCode == hotelCode).ToList();
                umbracoSettings.AlternativeHotelsOptions = umbracoSettings.AlternativeHotelsOptions.Where(x => x.OriginalHotel == hotelCode).ToList();
                umbracoSettings.AvailableHotelsAndRooms = umbracoSettings.AvailableHotelsAndRooms.Where(x => x.HotelCode == hotelCode).ToList();
                umbracoSettings.ConnectingDoorSettings = umbracoSettings.ConnectingDoorSettings.Where(x => x.HotelCode == hotelCode).ToList();
                umbracoSettings.RoomSeparateCombinations = umbracoSettings.RoomSeparateCombinations.Where(x => x.HotelCode == hotelCode).ToList();
            }

            return umbracoSettings
                ?? throw new ApiException(ServiceStatus.FatalError, $"{ApiHelper.LogTitle()} error: rootNodeId {rootNodeId} not defined in UmbracoSettings");
        }

        /// <summary>
        /// Get value from config by SettingKeys
        /// </summary>
        public static T GetValue_<T>(this IConfiguration configuration, SettingKeys settingKey)
        {
            try
            {
                T objectResult = (T)configuration.GetValue(settingKey, typeof(T)) ?? typeof(T).GetDefault<T>();
                return objectResult;
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                return typeof(T).GetDefault<T>();
            }
        }

        /// <summary>
        /// Get value from config by SettingKeys
        /// </summary>
        public static object GetValue(this IConfiguration configuration, SettingKeys settingKey, Type? resourceType = default)
        {
            object objectResult;
            try
            {
                resourceType ??= settingKey.GetResourceType();
                var config = (IConfigurationManager)configuration;

                var providerType = settingKey.GetProviderType();
                if (providerType == ProviderTypes.StaticList)
                {
                    objectResult = configuration.GetSection($"{SettingKeys.StaticData}:{settingKey}:Data").Get<string>()?.ToList() ?? [];
                }
                else
                {
                    var source = config.Sources.OfType<ApiConfigurationSource>().FirstOrDefault(x => x.SettingKey == settingKey);
                    objectResult = source != null && source.CurrentValue != null
                        ? source.CurrentValue
                        : resourceType.GetDefault();
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} exception: {MESSAGE}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                objectResult = settingKey.GetResourceType().GetDefault();
            }
            return objectResult;
        }

        /// <summary>
        /// Get ResourceType by SettingKeys 
        /// </summary>
        public static Type GetResourceType(this SettingKeys settingKey)
        {
            try
            {
                Type resourceType;
                var fieldInfo = typeof(SettingKeys).GetField(settingKey.ToString());
                var displayAttribute = fieldInfo?.GetCustomAttribute<DisplayAttribute>();
                resourceType = displayAttribute?.ResourceType!;
                return resourceType;
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} exception: {Exception}", ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message);
                return typeof(object);
            }
        }

        public static string GetProviderType(this SettingKeys settingKey)
        {
            try
            {
                var fieldInfo = typeof(SettingKeys).GetField(settingKey.ToString());
                var displayAttribute = fieldInfo?.GetCustomAttribute<DisplayAttribute>();
                var resourceType = displayAttribute?.GroupName ?? ProviderTypes.Unknown;
                return resourceType;
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} exception: {Exception}", ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// Get ResourceType by SettingKeys 
        /// </summary>
        public static Type GetResourceType(this string sourceType)
        {
            try
            {
                Type resourceType;
                var fieldInfo = typeof(ProviderTypes).GetField(sourceType);
                var displayAttribute = fieldInfo?.GetCustomAttribute<DisplayAttribute>();
                resourceType = displayAttribute?.ResourceType!;
                return resourceType;
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} exception: {Exception}", ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message);
                return typeof(object);
            }
        }

        /// <summary>
        /// Get SourceType by SourceTypes 
        /// </summary>
        public static string GetSourceType(this SettingKeys settingKey)
        {
            try
            {
                string sourceType;
                var fieldInfo = typeof(SettingKeys).GetField(settingKey.ToString());
                var displayAttribute = fieldInfo?.GetCustomAttribute<DisplayAttribute>();
                sourceType = displayAttribute?.GroupName!;
                return sourceType;
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} exception: {Exception}", ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message);
                return ProviderTypes.Unknown;
            }
        }

        /// <summary>
        /// GetJsonDefault - get default json value from ResourceType
        /// </summary>
        public static string GetJsonDefault(this SettingKeys settingKey)
        {
            return settingKey.GetResourceType().GetJsonDefault();
        }

        /// <summary> 
        /// Get all properties from enum attribute  
        /// </summary>
        public static DisplayAttribute GetDisplayAttribute(this SettingKeys settingKey)
        {
            try
            {
                var fieldInfo = typeof(SettingKeys).GetField(settingKey.ToString())!;
                var displayAttribute = fieldInfo.GetCustomAttribute<DisplayAttribute>()!;
                return displayAttribute;
            }
            catch (Exception ex)
            {
                throw new ApiException(ServiceStatus.FatalError, ex); ;
            }
        }

        /// <summary>
        /// Attempts to retrieve an instance of <see cref="OpenTelemetryConfig"/> used to configure the OpenTelemetry SDK.
        /// </summary>
        public static OpenTelemetryConfig GetOpenTelemetryOptions(this IConfigurationManager builder)
        {
            return builder.GetSection(OpenTelemetryConfig.ConfigSectionName).Get<OpenTelemetryConfig>()!;
        }

        /// <summary>
        /// CallGenericMethod - call generic method by name
        /// </summary>
        public static object? CallGenericMethod(this object obj, string methodName, Type type)
        {
            MethodInfo? methodInfo = obj.GetType().GetMethod(methodName);
            MethodInfo? genericMethod = methodInfo?.MakeGenericMethod(type);
            Type? returnType = methodInfo?.ReturnType;
            object? returnValue = genericMethod?.Invoke(obj, null);
            return returnType != null
                ? Convert.ChangeType(returnValue, returnType)
                : default;
        }

        #region private methods

        private static object? ConvertValue(string value, Type targetType)
        {
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                targetType = Nullable.GetUnderlyingType(targetType) ?? typeof(object);
            }
            return value != null ? Convert.ChangeType(value, targetType) : null;
        }

        private static object? ConvertValue(Type type, string value, string? path)
        {
            TryConvertValue(type, value, path, out object? result, out Exception? error);
            if (error != null)
            {
                throw error;
            }
            return result;
        }

        private static bool TryConvertValue(Type type, string value, string? path, out object? result, out Exception? error)
        {
            error = null;
            result = null;
            if (type == typeof(object))
            {
                result = value;
                return true;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                if (string.IsNullOrEmpty(value))
                {
                    return true;
                }
                return TryConvertValue(Nullable.GetUnderlyingType(type)!, value, path, out result, out error);
            }

            TypeConverter converter = TypeDescriptor.GetConverter(type);
            if (converter.CanConvertFrom(typeof(string)))
            {
                try
                {
                    result = converter.ConvertFromInvariantString(value);
                }
                catch (Exception ex)
                {
                    error = new InvalidOperationException(string.Format("Error_FailedBinding", path, type), ex);
                }
                return true;
            }

            if (type == typeof(byte[]))
            {
                try
                {
                    result = Convert.FromBase64String(value);
                }
                catch (FormatException ex)
                {
                    error = new InvalidOperationException(string.Format("Error_FailedBinding", path, type), ex);
                }
                return true;
            }

            return false;
        }

        #endregion private methods
    }
}
