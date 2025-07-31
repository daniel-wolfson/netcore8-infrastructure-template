using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Extensions;
using Custom.Framework.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Custom.Framework.Telemetry
{
    namespace Isrotel.Framework.Telemetry
    {
        public class ApiActivityFactory : IApiActivityFactory, IDisposable
        {
            public static Meter Meter = new(ApiHelper.ServiceName);
            private static Histogram<int> totalMemoryHistogram = Meter.CreateHistogram<int>("search.total.memory");
            private static Histogram<int> staticDataPriceCodesHistogram = Meter.CreateHistogram<int>("staticData.priceCodes.count");
            private static Counter<int> requestCount = Meter.CreateCounter<int>("search.request.count");

            //private readonly TelemetryClient _telemetryClient;
            private readonly ILogger _logger;
            private readonly ApiSettings _apiSettings;
            private readonly IHttpContextAccessor _httpContextAccessor;
            private ActivityListener _activityListener;

            public void SetTotalMemoryHistogram()
            {
                Process currentProcess = Process.GetCurrentProcess();
                long privateMemorySize = currentProcess.PrivateMemorySize64;
                totalMemoryHistogram.Record((int)(privateMemorySize / (1024 * 1024))); 
            }

            public Histogram<int> StaticDataPriceCodesHistogram 
            { 
                get => staticDataPriceCodesHistogram; 
            }

            public void AddRequestCount(int tick)
            {
                requestCount.Add(tick);
            }

            public ApiActivityFactory(
                IHttpContextAccessor httpContextAccessor, //TelemetryClient telemetryClient, 
                ILogger logger, IOptions<ApiSettings> apiSettings)
            {
                //_telemetryClient = telemetryClient;
                _logger = logger;
                _apiSettings = apiSettings.Value;
                _httpContextAccessor = httpContextAccessor;
                SetupActivityListener();
            }

            /// <summary>
            /// GetActivitySource - component publishing the tracing info
            /// </summary>
            public ActivitySource GetActivitySource(string name) => new(name);

            /// <summary>
            /// Creates a new <see cref="Activity"/> object if there is any listener to the Activity, returns null otherwise.
            /// </summary>
            //public Activity? CreateActivity(string name)
            //{
            //    var activity = new Activity(name);
            //    if (Activity.Current?.DisplayName == name)
            //        activity.Start();
            //    return activity;
            //}
            //public Activity? StopActivity(string name)
            //{
            //    if (Activity.Current?.DisplayName == name)
            //        Activity.Current.Stop();
            //    return Activity.Current;
            //}

            private void SetupActivityListener()
            {
                _activityListener = new ActivityListener
                {
                    ShouldListenTo = source => source.Name.Contains(ApiHelper.ServiceName),
                    Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,

                    ActivityStarted = activity =>
                    {
                        _logger.Information("Activity started: {OperationName}. ActivityId: {Id}", activity.OperationName, activity.Id);

                        //Azure Telemetry Client - write directly to azure
                        // var telemetry = new TraceTelemytry($"Activity started: {activity.OperationName}");
                        // telemetry.Context.Operation.Id = activity.RootId;
                        // telemetry.Context.Operation.ParentId = activity.ParentId;
                        // telemetry.Properties["ActivityId"] = activity.Id;
                        // _telemetryClient.TrackTrace(telemetry);

                        var context = _httpContextAccessor.HttpContext;
                        if (context != null)
                        {
                            var isDebugMode = context.IsRequestDebugMode();
                            var requestUrl = context.GetRequestFullPath();
                            var (controllerName, actionName) = context.GetContextData();
                            var requestData = context.Items.ContainsKey(HttpContextItemsKeys.RequestData) == true
                                ? context.Items[HttpContextItemsKeys.RequestData]?.ToString()
                                : string.Empty;

                            // Enrich the activity with additional tags
                            activity?.SetTag(DiagnosticActivityNames.ActivityId, activity.Id);
                            activity?.SetTag(DiagnosticActivityNames.OperationName, activity.OperationName);
                            activity?.SetTag(DiagnosticActivityNames.ControllerName, $"{ApiHelper.ServiceName}.{controllerName}");
                            activity?.SetTag(DiagnosticActivityNames.ActionName, actionName);
                            activity?.SetTag(DiagnosticActivityNames.RequestUrl, context.Request.Path.Value);
                            activity?.SetTag(DiagnosticActivityNames.CorrelationId, context.GetOrAddCorrelationHeader());
                            activity?.SetTag(DiagnosticActivityNames.RequestData, requestData);
                            activity?.SetTag(DiagnosticActivityNames.IsDebugMode, isDebugMode.ToString());
                            activity?.SetTag(DiagnosticActivityNames.RootId, activity.RootId);
                            activity?.SetTag(DiagnosticActivityNames.ParentId, activity.ParentId);
                        }
                    },

                    ActivityStopped = activity =>
                    {
                        _logger.Information("Activity ended: {OperationName} {Id} ActivityDuration: {Duration}", activity.OperationName, activity.Id, activity.Duration);

                        //Azure Telemetry Client - write directly to azure
                        //  var telemetry = new TraceTelemetry($"Activity ended: {activity.OperationName}");
                        //  telemetry.Context.Operation.Id = activity.RootId;
                        //  telemetry.Context.Operation.ParentId = activity.ParentId;
                        //  telemetry.Properties["ActivityId"] = activity.Id;
                        //  telemetry.Properties["Duration"] = activity.Duration.TotalMilliseconds.ToString();
                        //  _telemetryClient.TrackTrace(telemetry);

                        // Enrich the activity with additional tags
                        activity?.SetTag(DiagnosticActivityNames.ActivityId, activity.Id);
                        activity?.SetTag(DiagnosticActivityNames.DurationMs, activity.Duration.TotalMilliseconds);
                        activity?.SetTag(DiagnosticActivityNames.OperationId, activity.RootId);
                        activity?.SetTag(DiagnosticActivityNames.ParentId, activity.ParentId);
                    }
                };

                ActivitySource.AddActivityListener(_activityListener);
            }


            public void Dispose()
            {
                // Remove the listener by disposing of it
                _activityListener.Dispose();
            }
        }
    }
}