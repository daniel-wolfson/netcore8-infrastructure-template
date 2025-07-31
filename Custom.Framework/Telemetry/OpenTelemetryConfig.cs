using Asp.Versioning;
using Custom.Framework.Helpers;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Resources;

namespace Custom.Framework.Telemetry
{
    /// <summary>
    /// TODO: it is not completed
    /// Config options used to configure <see cref="TracerProviderBuilder"/> to send telemetry data to...
    /// </summary>
    public class OpenTelemetryConfig
    {
        private const string OtlpVersion = "1.5.0";
        private const string OtelExporterOtlpProtocolHttpProtobuf = "http/protobuf";
        private const string OtelExporterOtlpProtocolHttpJson = "http/json";
        private const string OtelExporterOtlpProtocolGrpc = "grpc";
        private const string ClassicKeyRegex = "^[a-f0-9]*$";
        private const string IngestClassicKeyRegex = "^hc[a-z]ic_[a-z0-9]*$";

        private bool isHttp = false;
        internal static readonly string SDefaultServiceName = ApiHelper.ServiceName 
            ?? $"Service:{System.Diagnostics.Process.GetCurrentProcess().ProcessName}";
        internal static readonly string SDefaultServiceVersion = ApiHelper.ServiceVersion;

        #region consts

        /// <summary>
        /// Name of the Honeycomb section of IConfiguration
        /// </summary>
        public const string ConfigSectionName = "OpenTelemetryConfig";

        /// <summary>
        /// Default API endpoint.
        /// </summary>
        public const string DefaultEndpoint = "https://api....:443";

        /// <summary>
        /// Default sample rate - sample everything.
        /// </summary>
        public const uint DefaultSampleRate = 1;

        #endregion consts

        #region props

        /// <summary>
        /// API key used to send telemetry data to Honeycomb.
        /// <para/>
        /// </summary>
        public string ApiKey { get; set; }

        private string _tracesCollectorUrl;
        public string TracesCollectorUrl
        {
            get { return _tracesCollectorUrl; }
            set { _tracesCollectorUrl = value + $"/v{ApiVersion.Default.MajorVersion}/traces"; }
        }

        private string _metricsCollectorUrl;
        public string MetricsCollectorUrl
        {
            get { return _metricsCollectorUrl; }
            set { _metricsCollectorUrl = value + $"/v{ApiVersion.Default.MajorVersion}/metrics"; }
        }

        private string _logsCollectorUrl;
        public string LogsCollectorUrl
        {
            get { return _logsCollectorUrl; }
            set { _logsCollectorUrl = value + $"/v{ApiVersion.Default.MajorVersion}/logs"; }
        }

        /// <summary>
        /// WriteToQueue links to honeycomb traces as they come in
        /// </summary>
        public bool EnableLocalVisualizations { get; set; } = false;

        /// <summary>
        /// API key used to send trace telemetry data to Honeycomb. Defaults to <see cref="ApiKey"/>.
        /// </summary>
        public string TracesApiKey { get; set; }

        /// <summary>
        /// API key used to send metrics telemetry data to Honeycomb. Defaults to <see cref="ApiKey"/>.
        /// </summary>
        public string MetricsApiKey { get; set; }

        /// <summary>
        /// Dataset to store telemetry data.
        /// <para/>
        /// </summary>
        public string Dataset { get; set; }

        /// <summary>
        /// Dataset to store trace telemetry data. Defaults to <see cref="Dataset"/>.
        /// </summary>
        public string TracesDataset { get; set; }

        /// <summary>
        /// Dataset to store metrics telemetry data. Defaults to "null".
        /// <para/>
        /// Required to enable metrics.
        /// </summary>
        public string MetricsDataset { get; set; }

        /// <summary>
        /// API endpoint to send telemetry data. Defaults to <see cref="DefaultEndpoint"/>.
        /// </summary>
        public string Endpoint { get; set; } = DefaultEndpoint;

        /// <summary>
        /// API endpoint to send telemetry data. Defaults to <see cref="Endpoint"/>.
        /// </summary>
        public string TracesEndpoint { get; set; }

        /// <summary>
        /// API endpoint to send telemetry data. Defaults to <see cref="Endpoint"/>.
        /// </summary>
        public string MetricsEndpoint { get; set; }

        /// <summary>
        /// Sample rate for sending telemetry data. Defaults to <see cref="DefaultSampleRate"/>.
        /// <para/>
        /// See <see cref="DeterministicSampler"/> for more details on how sampling is applied.
        /// </summary>
        public uint SampleRate { get; set; } = DefaultSampleRate;

        /// <summary>
        /// Unknown name used to identify application. Defaults to unknown_process:processname.
        /// </summary>
        public string ServiceName { get; set; } = SDefaultServiceName;

        /// <summary>
        /// Unknown version. Defaults to application assembly information version.
        /// </summary>
        public string ServiceVersion { get; set; } = SDefaultServiceVersion;

        /// <summary>
        /// (Optional) Additional <see cref="Meter"/> names for generating metrics.
        /// <see cref="ServiceName"/> is configured as a meter name by default.
        /// </summary>
        public List<string> MeterNames { get; set; } = new List<string>();

        /// <summary>
        /// The <see cref="ResourceBuilder" /> to use to add Resource attributes to.
        /// A custom ResouceBuilder can be used to set additional resources and then passed here to add
        /// Honeycomb attributes.
        /// </summary>
        public ResourceBuilder ResourceBuilder { get; set; } = ResourceBuilder.CreateDefault();

        /// <summary>
        /// Determines whether the <see cref="BaggageSpanProcessor"/> is added when configuring a <see cref="TracerProviderBuilder"/>.
        /// </summary>
        public bool AddBaggageSpanProcessor { get; set; } = true;

        /// <summary>
        /// Determines whether the <see cref="DeterministicSampler"/> sampler is added when configuring a <see cref="TracerProviderBuilder"/>.
        /// </summary>
        public bool AddDeterministicSampler { get; set; } = true;

        /// <summary>
        /// If set to true, enables the console span exporter for local debugging.
        /// </summary>
        public bool Debug { get; set; } = false;

        #endregion props

        #region Metrics

        /// <summary>
        /// Gets the <see cref="MetricsEndpoint" /> or falls back to the generic <see cref="Endpoint" />.
        /// </summary>
        internal string GetMetricsEndpoint()
        {
            var endpoint = new UriBuilder(Endpoint);
            if (isHttp && (string.IsNullOrWhiteSpace(endpoint.Path) || endpoint.Path == "/"))
            {
                endpoint.Path = MetricsCollectorUrl;
            }
            return MetricsEndpoint ?? endpoint.ToString();
        }

        internal string GetMetricsHeaders() => GetMetricsHeaders(MetricsApiKey, MetricsDataset);

        internal static string GetMetricsHeaders(string apikey, string dataset)
        {
            var headers = new List<string>
            {
                $"x-otlp-version={OtlpVersion}",
                $"x-isrotel-api={apikey}",
                $"x-isrotel-dataset={dataset}"
            };

            return string.Join(",", headers);
        }

        #endregion Metrics

        #region Traces

        internal string GetTracesDataset()
        {
            return TracesDataset ?? Dataset;
        }

        internal string GetTraceHeaders() => GetTraceHeaders(TracesApiKey, TracesDataset);

        internal static string GetTraceHeaders(string apikey, string dataset)
        {
            var headers = new List<string>
            {
                $"x-otlp-version={OtlpVersion}",
                $"x-honeycomb-team={apikey}"
            };

            if (!string.IsNullOrWhiteSpace(dataset))
            {
                headers.Add($"x-{ApiHelper.ServiceName}-dataset={dataset}");
            }

            return string.Join(",", headers);
        }

        #endregion Traces
    }
}