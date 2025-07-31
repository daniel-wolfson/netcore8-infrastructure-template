using Custom.Domain.Optima.Models.Availability;
using Custom.Domain.Optima.Models.Main;
using Custom.Framework.Configuration.Umbraco;
using Newtonsoft.Json;
using Serilog;

namespace Custom.Framework.Helpers
{
    public static class ApiConvertHelper
    {
        public static bool IsDebugMode { get; set; }

        /// <summary> Convert to OccupancyCode </summary>
        public static int ToOccupancyCode(this PackagesList p)
        {
            return p.Adults * 100 + p.Children * 10 + p.Infants;
        }

        /// <summary> Convert to BoardBase </summary>
        public static string ToBoardBase(this PlanData plan)
        {
            string pc = plan.Name.Substring(0, 2);
            switch (pc)
            {
                case "RO" or "HB" or "BB" or "AI":
                    return pc;
                default:
                    if (IsDebugMode)
                        Log.Logger.Warning("{TITLE} warning: planName '{PLANNAME}' not exists in settings and can't mapped.",
                            ApiHelper.LogTitle(), plan.Name);
                    return string.Empty;
            }
        }

        /// <summary> Convert from dictionary to list of UmbracoContent </summary>
        public static string ToUmbracoList(string? jsonResult)
        {
            var allSitesSettings = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonResult!);
            var umbracoSearchSettings = allSitesSettings!
                .Where(x => x.Value != null)
                .Select(x =>
                {
                    var umbracoSearchSettings = JsonConvert.DeserializeObject<UmbracoSettings>(x.Value)!;
                    _ = int.TryParse(x.Key, out int rootNodeId);
                    umbracoSearchSettings.Id = rootNodeId;
                    return umbracoSearchSettings;
                })
                .ToList();
            jsonResult = JsonConvert.SerializeObject(umbracoSearchSettings);
            return jsonResult;
        }

        /// <summary> ChangeType to resource type </summary>
        public static object? ChangeType(object data, Type resourceType)
        {
            object? objectResult = default;
            try
            {
                if (data == null)
                    return data;

                if (data.GetType() == typeof(string))
                    objectResult = JsonConvert.DeserializeObject(data.ToString() ?? "", resourceType);
                else
                    objectResult = Convert.ChangeType(data, resourceType);
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} exception on the resourceType deserialize: {RESOURCETYPE}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), resourceType.FullName, ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            return objectResult;
        }

        public static TData? ChangeType<TData>(object data)
        {
            try
            {
                Type settingType = typeof(TData);

                return typeof(TData?) == typeof(object)
                    ? (TData?)(object)data
                    : (TData?)Convert.ChangeType(data, settingType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ErrorInfo converting {data} to {typeof(TData)}: {ex.Message}");
                return default(TData?);
            }
        }

        /// <summary> Convert to adult count </summary>
        public static int Adult(this int occupancyCode)
        {
            return occupancyCode / 100;
        }

        /// <summary> Convert to children count </summary>
        public static int Children(this int occupancyCode)
        {
            return occupancyCode / 10 % 10;
        }

        /// <summary> Convert to infants count </summary>
        public static int Infants(this int occupancyCode)
        {
            return occupancyCode % 10;
        }
    }
}