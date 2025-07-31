using Custom.Framework.Models;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;

namespace Custom.Framework.Exceptions
{
    public class ApiRedisCacheException : ApiException
    {
        public ApiRedisCacheException(string message, 
            [CallerFilePath] string callerFilePath = "", 
            [CallerMemberName] string callerMemberName = "") 
            : base(message, callerFilePath, callerMemberName)
        {
        }

        public ApiRedisCacheException(Exception innerException, 
            [CallerFilePath] string callerFilePath = "", 
            [CallerMemberName] string callerMemberName = "") : base(innerException, callerFilePath, callerMemberName)
        {
        }

        public ApiRedisCacheException(string message, Exception innerException, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "") : base(message, innerException, callerFilePath, callerMemberName)
        {
        }

        public ApiRedisCacheException(ServiceStatus statusCode, Exception? innerException = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "") : base(statusCode, innerException, callerFilePath, callerMemberName)
        {
        }

        public ApiRedisCacheException(ServiceStatus statusCode, JObject errorObject, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "") : base(statusCode, errorObject, callerFilePath, callerMemberName)
        {
        }

        public ApiRedisCacheException(ServiceStatus statusCode, string message, Exception? innerException = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "") : base(statusCode, message, innerException, callerFilePath, callerMemberName)
        {
        }
    }
}
