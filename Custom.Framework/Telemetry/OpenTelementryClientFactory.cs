using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System.Diagnostics;

namespace Custom.Framework.Telemetry
{
    public class OpenTelementryClientFactory
    {
        private readonly ILogger logger;
        private readonly TelemetryClient telemetryClient;

        public OpenTelementryClientFactory(ILogger logger, TelemetryClient telemetryClient)
        {
            this.logger = logger;
            this.telemetryClient = telemetryClient;
            SetupActivityListener();
        }

        #region privates

        private void SetupActivityListener()
        {
            ActivitySource.AddActivityListener(new ActivityListener
            {
                ShouldListenTo = source => FilterActivitySource(source),
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity =>
                {
                    logger.Information("Started: {OperationName} {Id}", activity.OperationName, activity.Id);
                    var telemetry = new TraceTelemetry($"Activity started: {activity.OperationName}");
                    telemetry.Context.Operation.Id = activity.RootId;
                    telemetry.Context.Operation.ParentId = activity.ParentId;
                    telemetry.Properties["ActivityId"] = activity.Id;
                    telemetryClient.TrackTrace(telemetry);
                },
                ActivityStopped = activity =>
                {
                    logger.Information("Stopped: {OperationName} {Id} {Duration}", activity.OperationName, activity.Id, activity.Duration);
                    var telemetry = new DependencyTelemetry
                    {
                        Name = activity.OperationName,
                        Id = activity.Id,
                        Duration = activity.Duration,
                        Success = true
                    };
                    telemetry.Context.Operation.Id = activity.RootId;
                    telemetry.Context.Operation.ParentId = activity.ParentId;
                    telemetry.Properties["ActivityId"] = activity.Id;
                    telemetryClient.TrackDependency(telemetry);
                }
            });
        }

        private bool FilterActivitySource(ActivitySource source)
        {
            // Custom filter: only listen to activities from specific sources
            // Add more conditions as needed
            return source.Name == "YourDesiredActivitySourceName";
        }

        #endregion privates
    }
}