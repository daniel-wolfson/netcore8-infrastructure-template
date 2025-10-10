using Serilog;
using Serilog.Events;
using Serilog.Parsing;
using System.Text;
using Xunit.Abstractions;

namespace Custom.Framework.TestFactory.Core
{
    /// <summary> TestHostLogger </summary>
    public class TestHostLogger : ILogger
    {
        // delegate for output on Console or XUnitOutput
        private delegate void OutputWriterDelegate(string templateMessage, params object?[]? args);

        private readonly OutputWriterDelegate _outputWriter; // output method

        private readonly LogEventLevel[]? _outputSeverities = null;

        /// <summary> ctor </summary>
        public TestHostLogger()
        {
            _outputWriter = Console.WriteLine;
        }

        /// <summary> ctor </summary>
        public TestHostLogger(ITestOutputHelper? output) //, LogEventLevel[]? outputSeverities
        {
            _outputWriter = output != null ? output.WriteLine : Console.WriteLine;

            _outputSeverities = new LogEventLevel[] { //outputSeverities ?? 
                LogEventLevel.Information, LogEventLevel.Warning, LogEventLevel.Debug,
                LogEventLevel.Error, LogEventLevel.Fatal
            };
        }

        /// <summary> Debug </summary>
        public void Debug(string template, object? message = null)
        {
            Log(LogEventLevel.Warning, template);
        }

        /// <summary> Information </summary>
        public void Information(string template, object? message = null)
        {
            Log(LogEventLevel.Information, template, message!);
        }

        /// <summary> Error </summary>
        public void Error(string message)
        {
            Log(LogEventLevel.Error, message);
        }

        /// <summary> Error </summary>
        public void Error(Exception ex)
        {
            Log(LogEventLevel.Error, ex.InnerException?.Message ?? ex.Message ?? "");
        }

        /// <summary> Fatal </summary>
        public void Fatal(string message)
        {
            Log(LogEventLevel.Fatal, message);
        }

        /// <summary> Fatal </summary>
        public void Fatal(string message, Exception ex)
        {
            Log(LogEventLevel.Fatal, $"{ex.InnerException?.Message ?? ex.Message}; {message}");
        }

        /// <summary> Warning </summary>
        public void Warning(string message)
        {
            Log(LogEventLevel.Warning, message);
        }

        /// <summary> Warning </summary>
        public void Write(LogEvent logEvent)
        {
            var format = new StringBuilder();
            var messages = logEvent.Properties.Select(x => x.Value.ToString()).ToList() ?? [];
            var index = 0;

            foreach (var tok in logEvent.MessageTemplate.Tokens)
            {
                if (tok is not TextToken)
                    format.Append("{" + index++ + "}");
                else
                    format.Append(tok);
            }
            Log(logEvent.Level, logEvent.Timestamp.LocalDateTime, format.ToString(), messages.ToArray());
        }

        private void Log(LogEventLevel level, string template, params object[] messages)
        {
            Log(level, DateTime.Now, template, messages.ToArray());
        }

        private void Log(LogEventLevel level, DateTime time, string template, params object[] messages)
        {
            if (_outputSeverities != null && _outputSeverities.Contains(level))
            {
                var _outputWriterTitle = $"[{time:T} {ConvertLogEventLevelToString(level)}]";
                try
                {
                    _outputWriter($"{_outputWriterTitle} {template}", messages);
                }
                catch (Exception ex)
                {
                    _outputWriter($"{_outputWriterTitle} TestHostLogger.Log error: {ex.Message}");
                }
            }
        }

        /// <summary> BeginScope </summary>
        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public string ConvertLogEventLevelToString(LogEventLevel level)
        {
            switch (level)
            {
                case LogEventLevel.Verbose:
                    return "VRB";
                case LogEventLevel.Debug:
                    return "DBG";
                case LogEventLevel.Information:
                    return "INF";
                case LogEventLevel.Warning:
                    return "WRN";
                case LogEventLevel.Error:
                    return "ERR";
                case LogEventLevel.Fatal:
                    return "FTL";
                default:
                    return "UNK";
            }
        }
    }
}
