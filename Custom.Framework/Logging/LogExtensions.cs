using Custom.Framework.Exceptions;
using Custom.Framework.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Services;
using Serilog.Core;
using Serilog.Events;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Custom.Framework.Logging
{
    /// <summary>
    /// ActivityLogger extensions
    /// </summary>
    public static class LogExtensions
    {
        private static readonly object[] NoPropertyValues = [];

        [MessageTemplateFormatMethod("messageTemplate")]
        public static void Add(this ILogger logger,
            string messageTemplate,
            ApiException? exception = null,
            [CallerFilePath] string className = "",
            [CallerMemberName] string methodName = "")
        {
            var activityLogger = logger as ApiActivityLogger;
            activityLogger?.AddLog(LogEventLevel.Verbose, messageTemplate, exception, className, methodName);
        }

        [MessageTemplateFormatMethod("messageTemplate")]
        public static void Add<T0>(this ILogger logger,
            LogEventLevel level,
            string messageTemplate,
            T0 propertyValue0,
            ApiException? exception = null,
            [CallerFilePath] string className = "",
            [CallerMemberName] string methodName = "")
        {
            var args = new List<object>();
            if (propertyValue0 != null) args.Add(propertyValue0);

            var activityLogger = logger as ApiActivityLogger;
            activityLogger?.AddLog(level, messageTemplate, exception, className, methodName, [.. args]);
        }

        /// <summary>
        /// WriteToQueue a log event with the <see cref="LogEventLevel.Information"/> level.
        /// </summary>
        [MessageTemplateFormatMethod("messageTemplate")]
        public static void Add<T0>(this ILogger logger,
            string messageTemplate,
            T0 propertyValue0,
            ApiException? exception = null,
            [CallerFilePath] string className = "",
            [CallerMemberName] string methodName = "")
        {
            var args = new List<object>();
            if (propertyValue0 != null) args.Add(propertyValue0);

            var activityLogger = logger as ApiActivityLogger;
            activityLogger?.AddLog(LogEventLevel.Information, messageTemplate, exception, className, methodName, args.ToArray());
        }

        [MessageTemplateFormatMethod("messageTemplate")]
        public static void Add<T0, T1>(this ILogger logger,
            string messageTemplate,
            T0 propertyValue0, T1 propertyValue1,
            ApiException? exception = null,
            [CallerFilePath] string className = "",
            [CallerMemberName] string methodName = "")
        {
            var args = new ArrayList();
            if (propertyValue0 != null) args.Add(propertyValue0);
            if (propertyValue1 != null) args.Add(propertyValue1);
            var activityLogger = logger as ApiActivityLogger;
            activityLogger?.AddLog(LogEventLevel.Information, messageTemplate, exception, className, methodName, args);
        }

        [MessageTemplateFormatMethod("messageTemplate")]
        public static void Add<T0, T1, T2>(this ILogger logger,
            string messageTemplate,
            T0 propertyValue0, T1 propertyValue1, T2 propertyValue2,
            ApiException? exception = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string methodName = "")
        {
            var args = new ArrayList();
            if (propertyValue0 != null) args.Add(propertyValue0);
            if (propertyValue1 != null) args.Add(propertyValue1);
            if (propertyValue2 != null) args.Add(propertyValue2);

            var activityLogger = logger as ApiActivityLogger;
            activityLogger?.AddLog(LogEventLevel.Information, messageTemplate, exception, callerFilePath, methodName, args);
        }

        [MessageTemplateFormatMethod("messageTemplate")]
        public static void Add<T0, T1, T2, T3>(this ILogger logger,
            string messageTemplate,
            T0 propertyValue0, T1 propertyValue1, T2 propertyValue2, T3 propertyValue3,
            ApiException? exception = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            var args = new ArrayList();
            if (propertyValue0 != null) args.Add(propertyValue0);
            if (propertyValue1 != null) args.Add(propertyValue1);
            if (propertyValue2 != null) args.Add(propertyValue2);
            if (propertyValue3 != null) args.Add(propertyValue3);

            var activityLogger = logger as ApiActivityLogger;
            activityLogger?.AddLog(LogEventLevel.Information, messageTemplate, exception, callerFilePath, callerMemberName, args);
        }

        [MessageTemplateFormatMethod("messageTemplate")]
        private static void Log(ILogger logger, LogEventLevel logEvent, string messageTemplate,
            [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "",
            params object?[]? propertyValues)
        {
            var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);
            var contextName = $"{callerTypeName}.{callerMemberName}";
            logger
            .ForContext("Step", contextName)
                .Write(logEvent, $"Step {{STEP}}. {messageTemplate}", propertyValues);
        }
    }
}
