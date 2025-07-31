using Custom.Framework.Configuration.Models;
using Custom.Framework.Exceptions;
using Custom.Framework.Extensions;
using Custom.Framework.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Services;
using Serilog.Events;
using Serilog.Parsing;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Custom.Framework.Logging
{
    // This zero latency, thread-safe class queues logs and writes them on a ThreadPool thread.  This avoids blocking the caller to wait for I/O.
    public class ApiActivityLogger : ILogger, IDisposable
    {
        private const int _queueIntervalMsec = 100;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private BlockingCollection<LogEvent> _traceQueue;
        private bool _disposed;
        private ILogger _logger;
        private int _latencyStep = 1;

        public LogEventLevel MinTraceLogLevel { get; private set; } = LogEventLevel.Debug;

        public string CorrelationId { get; private init; }

        public ApiActivityLogger(IHttpContextAccessor httpContextAccessor, ILogger logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            CorrelationId = _httpContextAccessor.HttpContext?.GetRequestHeader(RequestHeaderKeys.CorrelationId)?.Replace("-", "")
                ?? Guid.NewGuid().ToString();
            _traceQueue = new BlockingCollection<LogEvent>(new ConcurrentQueue<LogEvent>());

            if (_httpContextAccessor.HttpContext != null && !_httpContextAccessor.HttpContext.Items.ContainsKey("ActivityLogs"))
                _httpContextAccessor.HttpContext?.Items.Add("ActivityLogs", _traceQueue);
            else if (_httpContextAccessor.HttpContext != null)
                _traceQueue = (BlockingCollection<LogEvent>)_httpContextAccessor?.HttpContext?.Items["ActivityLogs"]!;
        }

        public void AddLog(string templateMessage,
           Exception? exception = null,
           [CallerFilePath] string callerFilePath = "",
           [CallerMemberName] string callerMemberName = "",
           params object[]? args)
        {
            Log(LogEventLevel.Information, templateMessage, exception, callerFilePath, callerMemberName);
        }

        public void AddLog(LogEventLevel level, string templateMessage,
           Exception? exception = null,
           [CallerFilePath] string callerFilePath = "",
           [CallerMemberName] string callerMemberName = "",
           params object[]? args)
        {
            Log(level, templateMessage, exception, callerFilePath, callerMemberName, args);
        }

        public void ErrorLog(string templateMessage,
            ApiException? exception = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            Log(LogEventLevel.Error, templateMessage, exception, callerFilePath, callerMemberName);
        }

        public void ErrorLog(string templateMessage,
            Exception? exception = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            Log(LogEventLevel.Error, templateMessage, exception, callerFilePath, callerMemberName);
        }

        public void ErrorLog(ApiException exception,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            Log(LogEventLevel.Error, string.Empty, exception, callerFilePath, callerMemberName);
        }

        public void ErrorLog(Exception ex)
        {
            _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                   ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
        }

        public void Fatal(string templateMessage,
            ApiException? exception = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            Log(LogEventLevel.Fatal, templateMessage, exception, callerFilePath, callerMemberName);
        }

        public void Warning(string templateMessage,
            ApiException? exception = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            Log(LogEventLevel.Warning, templateMessage, exception, callerFilePath, callerMemberName);
        }

        public void Information(string templateMessage,
            ApiException? exception = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            Log(LogEventLevel.Information, templateMessage, exception, callerFilePath, callerMemberName);
        }

        public void Write(LogEvent logEvent)
        {
            if (_logger.IsEnabled(MinTraceLogLevel) || logEvent.Level == LogEventLevel.Verbose)
            {
                var steps = _traceQueue
                    .SelectMany(x => x.MessageTemplate.Text)
                    .Select(x => x.ToString())
                    .ToList() ?? [];

                if (!steps.Contains(logEvent.MessageTemplate.Text))
                {
                    _traceQueue.TryAdd(logEvent);
                }
            }
        }

        private void Log(LogEventLevel level, string templateMessage,
            Exception? exception = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "",
            params object[] args)
        {
            Task.Run(() =>
            {
                try
                {
                    if (level != LogEventLevel.Verbose)
                        Interlocked.Increment(ref _latencyStep);

                    var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);
                    var msgTitle = $"{callerTypeName}.{callerMemberName} step{_latencyStep}. {templateMessage} - {{LatencyTime}}";
                    var messageTemplateTokens = new MessageTemplateParser()
                        .Parse(msgTitle)
                        .Tokens.Where(x => x.ToString()?.StartsWith("{") ?? false)
                        .ToArray();
                    var messageTemplate = new MessageTemplate(msgTitle, messageTemplateTokens);

                    var propertiesList = new List<LogEventProperty>();
                    _logger = _logger.ForContext("step", msgTitle);
                    propertiesList.Add(new LogEventProperty("step", new ScalarValue(_latencyStep)));

                    for (int i = 0; i < messageTemplateTokens.Length; i++)
                    {
                        if (args.Length != 0 && args[i] != null)
                        {
                            var propertyName = ((PropertyToken)messageTemplateTokens[i]).PropertyName;
                            propertiesList.Add(new LogEventProperty(propertyName, new ScalarValue(args[i])));
                        }
                    }

                    var correlationIdHex = CorrelationId.Replace("-", "");
                    var traceId = ActivityTraceId.CreateFromString(correlationIdHex.AsSpan());

                    var eventLog = new LogEvent(
                        spanId: ActivitySpanId.CreateRandom(),
                        traceId: traceId,
                        timestamp: DateTimeOffset.Now,
                        level: level,
                        exception: exception,
                        messageTemplate: messageTemplate,
                        properties: propertiesList
                    );

                    Write(eventLog);
                }
                catch (Exception ex)
                {
                    _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                        ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
                }
            });
        }

        private void WriteLog(LogEvent logEvent)
        {
            if (_logger.IsEnabled(MinTraceLogLevel))
            {
                _logger.Write(logEvent);
            }
        }

        private void TraceLog()
        {
            try
            {
                if (_traceQueue.Count > 0)
                {
                    _traceQueue.CompleteAdding();
                    _logger.Information("");
                }

                LogEvent? stepFirst = default;
                LogEvent? stepLast = default;

                foreach (var log in _traceQueue)
                {
                    var step = int.Parse(log.Properties["step"]?.ToString() ?? "");
                    if (step == 1)
                    {
                        stepFirst = log;
                        var stepFirstLog = MakeLog(stepFirst, stepFirst.MessageTemplate.Text, 0.0);
                        //WriteLog(stepFirstLog);
                    }
                    else if (step > 1)
                    {
                        var stepPrev = _traceQueue.FirstOrDefault(x => int.Parse(x.Properties["step"].ToString()) == step - 1);
                        if (stepPrev != null)
                        {
                            stepLast = log;
                            var diff = stepLast.Timestamp - stepPrev.Timestamp;
                            var stepLastLog = MakeLog(stepLast, stepLast.MessageTemplate.Text, diff);
                            //WriteLog(stepLastLog);
                        }
                    }

                    // log for details (level = verbose)
                    var details = _traceQueue.Where(x => x.Level == LogEventLevel.Verbose);
                    if (details.Any())
                    {
                        foreach (var detail in details)
                        {
                            var detailLog = MakeLog(detail, "verbose", detail.MessageTemplate.Text);
                            if (detailLog != null)
                            {
                                var currentActivity = Activity.Current;
                                currentActivity?.AddEvent(new ActivityEvent(detailLog?.ToString() ?? detail.MessageTemplate.Text,
                                    tags: new ActivityTagsCollection
                                    {
                                        { $"{currentActivity.DisplayName}_serviceName", ApiHelper.ServiceName },
                                        { $"{currentActivity.DisplayName}_details", detail.MessageTemplate.Text }
                                    }));

                                //WriteLog(detailLog!);
                            }
                        }
                    }
                }

                if (stepFirst != null && stepLast != null)
                {
                    var diff = (stepLast.Timestamp - stepFirst.Timestamp).TotalSeconds;
                    var logTotal = MakeLog(stepLast, "Total", diff);
                    //WriteLog(logTotal);
                }

                while (_traceQueue.Count > 0)
                {
                    _traceQueue.Take();
                }

                _logger.Information("");
                _latencyStep = 0;
            }
            catch (InvalidOperationException ex)
            {
                _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                   ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
            finally
            {
                _traceQueue?.Dispose();
                _traceQueue = null!;
                Activity.Current?.Stop();
            }
        }

        protected LogEvent MakeLog(LogEvent originalLogEvent, string propertyName, object propertyValue)
        {
            var props = originalLogEvent.Properties.Select(x => new LogEventProperty(x.Key, x.Value)).ToList();
            props.Add(new LogEventProperty(propertyName, new ScalarValue(propertyValue)));

            return new LogEvent(
                originalLogEvent.Timestamp,
                originalLogEvent.Level,
                originalLogEvent.Exception,
                originalLogEvent.MessageTemplate,
                props,
                (ActivityTraceId)originalLogEvent.TraceId!,
                (ActivitySpanId)originalLogEvent.SpanId!);
        }

        #region private methods

        private IEnumerable<string> GetTokenNames(MessageTemplateToken[] messageTemplateTokens)
        {
            foreach (var token in messageTemplateTokens)
            {
                yield return token?.ToString() ?? string.Empty;
                break;
            }
        }

        #endregion private methods

        #region dispose implementation

        // See Microsoft-recommended dispose pattern at https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose.
        ~ApiActivityLogger() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
            Debug.Assert(_disposed, "Dispose()");
        }

        protected virtual void Dispose(bool disposing)
        {
            //Debug.Assert(_disposed, "Dispose(bool disposing)");
            if (_disposed) return;

            Thread.Sleep(_queueIntervalMsec); // Wait for queue's complete

            if (_traceQueue.Count > 0)
                TraceLog();

            // Free managed objects.
            if (disposing)
            {
            }

            // Free unmanaged objects.
            _disposed = true;
        }

        #endregion dispose implementation
    }
}


