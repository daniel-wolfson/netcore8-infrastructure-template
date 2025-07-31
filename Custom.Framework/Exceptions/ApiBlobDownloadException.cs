using Custom.Framework.Models;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;

namespace Custom.Framework.Exceptions
{
    public class ApiBlobDownloadException : ApiException
    {
        public ApiBlobDownloadException(string message, 
            [CallerFilePath] string callerFilePath = "", 
            [CallerMemberName] string callerMemberName = "") 
            : base(message, callerFilePath, callerMemberName)
        {
        }

        public ApiBlobDownloadException(Exception innerException, 
            [CallerFilePath] string callerFilePath = "", 
            [CallerMemberName] string callerMemberName = "") 
            : base(innerException, callerFilePath, callerMemberName)
        {
        }

        public ApiBlobDownloadException(string message, Exception innerException,
            [CallerFilePath] string callerFilePath = "", 
            [CallerMemberName] string callerMemberName = "") 
            : base(message, innerException, callerFilePath, callerMemberName)
        {
        }

        public ApiBlobDownloadException(ServiceStatus statusCode, Exception? innerException = null, 
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "") 
            : base(statusCode, innerException, callerFilePath, callerMemberName)
        {
        }

        public ApiBlobDownloadException(ServiceStatus statusCode, JObject errorObject, 
            [CallerFilePath] string callerFilePath = "", 
            [CallerMemberName] string callerMemberName = "") 
            : base(statusCode, errorObject, callerFilePath, callerMemberName)
        {
        }

        public ApiBlobDownloadException(ServiceStatus statusCode, string message, Exception? innerException = null, 
            [CallerFilePath] string callerFilePath = "", 
            [CallerMemberName] string callerMemberName = "") 
            : base(statusCode, message, innerException, callerFilePath, callerMemberName)
        {
        }
    }
}
