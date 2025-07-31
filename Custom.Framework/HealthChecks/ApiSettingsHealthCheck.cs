//using Custom.Framework.Config;
//using Custom.Framework.Config.Models;
//using Custom.Framework.Config.Optima;
//using Custom.Framework.Helpers;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AppBuilderExtensions.Config;
//using Microsoft.AppBuilderExtensions.DependencyInjection;
//using Microsoft.AppBuilderExtensions.Diagnostics.HealthChecks;
//using Microsoft.AppBuilderExtensions.Options;
//using Newtonsoft.Json;
//using System.Collections;
//using System.Dynamic;
//using System.Reflection;
//using ErrorInfo = Custom.Framework.Models.Errors.ErrorInfo;

//namespace Custom.Framework.HealthChecks
//{
//    public class ApiSettingsHealthCheck(
//        IConfiguration configuration,
//        ILogger logger, IHttpContextAccessor httpContextAccessor,
//        IOptions<ApiSettings> appSettingsOptions) : IHealthCheck
//    {
//        private readonly ILogger _logger = logger;
//        private readonly IConfiguration _config = configuration;
//        private readonly ApiSettings _appSettings = appSettingsOptions.Value;
//        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

//        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
//            CancellationToken cancellationToken = default)
//        {
//            var staticDataService = httpContextAccessor.HttpContext.RequestServices.GetService<IStaticDataService>()!;
//            await staticDataService.SetupConfiguration();

//            var settingItems = _config.GetSections(SettingKeys.StaticList).ToList();
//            var data = new Dictionary<string, object>();
//            var isHealthy = false;
//            var request = _httpContextAccessor.HttpContext?.Request;
//            var url = $"{request?.Scheme}://{request?.Host}{request?.Path}{request?.QueryString}";

//            foreach (var settingItem in settingItems)
//            {
//                var detailsUrl = $"{url}/ApiSettings/{settingItem.SettingKey}";
//                var verSettingKey = $"{_appSettings.Version}/{settingItem.SettingKey}";

//                switch (settingItem.SettingKey)
//                {
//                    case SettingKeys.OptimaSettings:
//                        var optimaSettingsValue = settingItem.Value as OptimaSettings;
//                        isHealthy = optimaSettingsValue?.SitesSettings.Count > 0 && optimaSettingsValue?.CodesConversion.Count > 0;
//                        settingItem.IsValid = isHealthy;
//                        settingItem.Healthness = isHealthy ? $"Healthy  (SitesSettings.Count={optimaSettingsValue?.SitesSettings.Count}, CodesConversion.Count={optimaSettingsValue?.CodesConversion.Count})" : "Unhealthy";
//                        settingItem.Value = isHealthy ? JsonConvert.SerializeObject(optimaSettingsValue) : "";
//                        settingItem.Data = Convert.ChangeType(optimaSettingsValue, settingItem.SettingKey.GetResourceType());
//                        break;

//                    default:
//                        var collection = settingItem.Value as ICollection;
//                        isHealthy = collection != null && collection.Count > 0;

//                        if (!string.IsNullOrEmpty(settingItem.DependsOn))
//                        {
//                            var relatedSettingItemData = settingItem.DependsOn.ToString()?.Split(":") ?? [];
//                            var relatedSettingItemKey = relatedSettingItemData[0];
//                            var relatedSettingItemProperty = relatedSettingItemData[1];
//                            var relatedSettingItemSettingKey = Enum.Parse<SettingKeys>(relatedSettingItemKey);
//                            var relatedSettingItemValues = settingItems.First(x => x.SettingKey == relatedSettingItemSettingKey).Value;
//                            var relatedSettingItemDataValues = Convert.ChangeType(relatedSettingItemValues, relatedSettingItemSettingKey.GetResourceType());

//                            if (settingItem.Value != null && relatedSettingItemValues != null)
//                            {

//                                if (relatedSettingItemKey == SettingKeys.PriceCodes.ToString()
//                                    && settingItem.Value.GetType().GetInterface(nameof(IEnumerable)) != null)
//                                {
//                                    var currentData = settingItem.Value as ICollection;
//                                    settingItem.Data = JsonConvert.SerializeObject(settingItem.Value);
//                                    var relatedData = relatedSettingItemDataValues as ICollection;

//                                    var diff = GetListDiffByProperty("PriceCode", relatedData, currentData)
//                                        .Cast<string>().Distinct().ToList();
//                                    //var intersectedValues = relatedDataPriceCodes.Except(currentDataPriceCodes).ToList();
//                                    settingItem.ValidationErrors = ErrorInfo.Validation(
//                                        $"Except.Validation between {relatedSettingItemKey}(see ValidationResult) and {settingItem.SettingKey}",
//                                        $"{string.Join(", ", diff)}")
//                                        .ToJsonValidationResult();
//                                }
//                            }
//                        }
//                        else
//                        {
//                            settingItem.Value = isHealthy ? JsonConvert.SerializeObject(collection) : "";
//                            settingItem.Data = JsonConvert.SerializeObject(collection);
//                        }

//                        settingItem.IsValid = isHealthy;
//                        settingItem.Healthness = isHealthy ? $"Healthy  (Count={collection?.Count ?? 0})" : "Unhealthy";

//                        break;
//                }

//                if (!isHealthy)
//                    _logger.Warning("{TITLE} got {VERSION}/{SETTINGKEY}: {DATA}",
//                        ApiHelper.LogTitle(), _appSettings.Version, settingItem.SettingKey, JsonConvert.SerializeObject(settingItem.Value));
//            }

//            data = settingItems.ToKeyValueList(
//                k => k.SettingKey.ToString(),
//                v =>
//                {
//                    dynamic expando = new ExpandoObject();
//                    expando.Data = v.Data;
//                    expando.Errors = v.ValidationErrors ?? "";
//                    return expando;
//                });


//            if (settingItems.All(x => !x.Healthness?.ToString()?.Contains("Unhealthy") ?? false))
//            {
//                return Task.FromResult(HealthCheckResult.Healthy(
//                    description: $"all settingItems are healthy",
//                    data: data));
//            }

//            return Task.FromResult(new HealthCheckResult(
//                context.Registration.FailureStatus,
//                "At least one or more of Settings is null or empty, see details in data",
//                data: data));
//        }

//        public static List<object> GetListDiffByProperty(string propertyName,
//            object listObj1, object listObj2)
//        {
//            var listDiff = new List<object>();
//            if (listObj1 is IList list1 && listObj2 is IList list2)
//            {
//                Type listType1 = list1.GetType();
//                Type listType2 = list2.GetType();

//                if (listType1.IsGenericType && listType1.GetGenericTypeDefinition() == typeof(List<>))
//                {
//                    Type genericType1 = listType1.GetGenericArguments()[0];
//                    Type genericType2 = listType2.GetGenericArguments()[0];

//                    PropertyInfo? propInfo1 = genericType1.GetProperty(propertyName);
//                    PropertyInfo? propInfo2 = genericType2.GetProperty(propertyName);

//                    if (propInfo1 == null || propInfo2 == null)
//                        throw new ArgumentException($"Property {propertyName} not found on one of the types.");

//                    var genericListType1 = typeof(List<>).MakeGenericType(genericType1);
//                    var genericListType2 = typeof(List<>).MakeGenericType(genericType2);
//                    var values2 = new List<object>();

//                    // Add items from the original list to the new list
//                    foreach (var item in list1)
//                    {
//                        listDiff.Add(propInfo1.GetValueOrDefault(item));
//                    }

//                    // Get the values of the property from the lists
//                    //var values1 = newList1.Select(x => propInfo1.GetValueOrDefault(x)).ToList();
//                    //var values2 = list2.Select(x => propInfo2.GetValueOrDefault(x)).ToList();
//                    // Find the values that are in the first list but not in the second
//                    var difference = listDiff.Except(values2).ToList();
//                }
//                else
//                {
//                    Console.WriteLine("The object is a non-generic list.");
//                }
//            }

//            return listDiff;
//        }
//    }
//}