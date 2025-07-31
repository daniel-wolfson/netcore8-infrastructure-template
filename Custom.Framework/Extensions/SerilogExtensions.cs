using Serilog.Events;
using System.Runtime.CompilerServices;

namespace Custom.Framework.Extensions
{
    public static class SerilogExtensions
    {
        public static void InfoExt(this ILogger logger,
        string message, Exception ex,
        [CallerFilePath] string callerFilePath = "",
        [CallerMemberName] string callerMemberName = "")
        {
            logger.Log(LogEventLevel.Information, "", message, ex, callerFilePath, callerMemberName);
        }

        public static void WarningExt(this ILogger logger,
        string message, Exception ex,
        [CallerFilePath] string callerFilePath = "",
        [CallerMemberName] string callerMemberName = "")
        {
            logger.Log(LogEventLevel.Warning, null, message, ex, callerFilePath, callerMemberName);
        }

        public static void ErrorExt(this ILogger logger,
        Exception ex,
        [CallerFilePath] string callerFilePath = "",
        [CallerMemberName] string callerMemberName = "")
        {
            logger.Log(LogEventLevel.Error, null, null, ex, callerFilePath, callerMemberName);
        }

        public static void ErrorExt(this ILogger logger,
        string message, Exception? ex = null,
        [CallerFilePath] string callerFilePath = "",
        [CallerMemberName] string callerMemberName = "")
        {
            var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);
            var title = $"{callerTypeName}.{callerMemberName}.";

            logger.Log(LogEventLevel.Error, title, message, ex);
        }

        public static void ErrorExt(this ILogger logger,
        string title, object[] args,
        [CallerFilePath] string callerFilePath = "",
        [CallerMemberName] string callerMemberName = "")
        {
            var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);
            title = $"{callerTypeName}.{callerMemberName}{title}:";

            logger.Log(LogEventLevel.Error, title, "");
        }

        /// <summary>
        /// Log - method is an extension method for the ILogger interface, 
        /// allowing it to be called on instances of ILogger.
        /// The method takes several parameters:
        /// <para>•	level of type LogEventLevel: This parameter represents the log event level, indicating the severity of the log message.</para>
        /// <para>•	title of type string: This parameter represents the title or additional information for the log message.</para>
        /// <para>•	message of type string: This parameter represents the main message of the log.</para>
        /// <para>•	ex of type Exception?: This parameter represents an optional exception object associated with the log message.</para>
        /// <para>•	[CallerFilePath] string callerFilePath = "": This parameter is an optional parameter that is automatically populated with the file path of the calling code file.</para>
        /// <para>•	[CallerMemberName] string callerMemberName = "": This parameter is an optional parameter that is automatically populated with the name of the calling member (method or property).</para>
        /// </summary>
        private static void Log(this ILogger logger, LogEventLevel level,
        string? title, string? message, Exception? ex = null,
        [CallerFilePath] string callerFilePath = "",
        [CallerMemberName] string callerMemberName = "")
        {
            var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);
            title = !string.IsNullOrEmpty(title) ? title : $"{callerTypeName}.{callerMemberName}.";
            List<KeyValuePair<string, string>> args = [];

            if (!string.IsNullOrEmpty(title))
                args.Add(new KeyValuePair<string, string>("{TITLE}.", title));

            if (!string.IsNullOrEmpty(title))
                args.Add(new KeyValuePair<string, string>("Message: {MESSAGE}.", message));

            if (ex != null)
            {
                args.Add(new KeyValuePair<string, string>("ErrorInfo: {ERROR}.", ex?.InnerException?.Message ?? ex?.Message ?? ""));
                args.Add(new KeyValuePair<string, string>("StackTrace: {STACKTRACE}.", ex?.StackTrace ?? ""));
            }

            var templateArgs = string.Join(" ", args.Select(arg => "{" + $"{arg.Key}: {arg.Value}" + "}"));
            var valueArgs = args.Select(x => x.Value).ToArray();

            if (!string.IsNullOrEmpty(templateArgs) && valueArgs.Length != 0)
                logger.Write(level, templateArgs, valueArgs);
        }
    }
}
