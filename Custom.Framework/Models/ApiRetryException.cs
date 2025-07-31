using System.Runtime.CompilerServices;
using Custom.Framework.Exceptions;

namespace Custom.Framework.Models
{
    public class ApiRetryException : ApiException
    {
        public ApiRetryException(ServiceStatus statusCode, string message,
            [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
            : base(statusCode, message)
        {
            CallerFilePath = callerFilePath;
            CallerMemberName = callerMemberName;
        }
    }
}
