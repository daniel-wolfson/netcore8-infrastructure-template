using System.Reflection;

namespace Custom.Framework.Core
{
    public class ApiHttpClientNames
    {
        public static string OptimaAvailabilityApi = "OptimaAvailabilityApi";
        public static string OptimaReservationApi = "OptimaReservationApi";
        public static string OptimaCustomerApi = "OptimaCustomerApi";
        public static string OptimaMainApi = "OptimaMainApi";
        public static string OptimaSunClubApi = "OptimaSunClubApi";
        public static string OperaSunClubApi = "OperaSunClubApi";
        public static string UmbracoApi = "UmbracoApi";
        public static string DalApi = "DalApi";
        public static string NotificationsApi = "NotificationsApi";
        public static string TransactionLogApi = "TransactionLogApi";
        public static string ArkiaApi = "ArkiaApi";
        public static string OptimaStaticDataApi = "OptimaStaticDataApi";
        public static string NopApi = "NopApi";

        public static List<string> GetNames()
        {
            return typeof(ApiHttpClientNames)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string))
                .Select(f => f.GetValue(null)?.ToString() ?? "")
                .Where(value => string.IsNullOrWhiteSpace(value))
                .ToList();
        }
    }
}
