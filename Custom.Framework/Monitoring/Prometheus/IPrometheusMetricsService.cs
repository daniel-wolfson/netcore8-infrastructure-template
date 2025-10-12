using Prometheus;

namespace Custom.Framework.Monitoring.Prometheus;

/// <summary>
/// Interface for Prometheus metrics manager
/// Provides methods to create and record various metric types
/// </summary>
public interface IPrometheusMetricsService
{
    /// <summary>
    /// Create or get a counter metric
    /// Counters only increase and are typically used for counting requests, errors, etc.
    /// </summary>
    Counter CreateCounter(string name, string help, params string[] labelNames);

    /// <summary>
    /// Create or get a gauge metric
    /// Gauges can go up and down and are typically used for measurements like memory, queue size, etc.
    /// </summary>
    Gauge CreateGauge(string name, string help, params string[] labelNames);

    /// <summary>
    /// Create or get a histogram metric
    /// Histograms track the distribution of observations (e.g., request durations)
    /// </summary>
    Histogram CreateHistogram(string name, string help, double[]? buckets = null, params string[] labelNames);

    /// <summary>
    /// Create or get a summary metric
    /// Summaries track the distribution with configurable quantiles
    /// </summary>
    Summary CreateSummary(string name, string help, params string[] labelNames);

    /// <summary>
    /// Increment a counter by 1
    /// </summary>
    void IncrementCounter(string name, params string[] labelValues);

    /// <summary>
    /// Increment a counter by a specific value
    /// </summary>
    void IncrementCounter(string name, double value, params string[] labelValues);

    /// <summary>
    /// Set a gauge to a specific value
    /// </summary>
    void SetGauge(string name, double value, params string[] labelValues);

    /// <summary>
    /// Increment a gauge
    /// </summary>
    void IncrementGauge(string name, double value = 1, params string[] labelValues);

    /// <summary>
    /// Decrement a gauge
    /// </summary>
    void DecrementGauge(string name, double value = 1, params string[] labelValues);

    /// <summary>
    /// Observe a histogram value (typically duration in seconds)
    /// </summary>
    void ObserveHistogram(string name, double value, params string[] labelValues);

    /// <summary>
    /// Observe a summary value
    /// </summary>
    void ObserveSummary(string name, double value, params string[] labelValues);

    /// <summary>
    /// Track the duration of an operation using a histogram
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="action">Action to measure</param>
    /// <param name="labelValues">Label values</param>
    void TrackDuration(string name, Action action, params string[] labelValues);

    /// <summary>
    /// Track the duration of an async operation using a histogram
    /// </summary>
    Task TrackDurationAsync(string name, Func<Task> action, params string[] labelValues);

    /// <summary>
    /// Track the duration of an operation and return its result
    /// </summary>
    T TrackDuration<T>(string name, Func<T> func, params string[] labelValues);

    /// <summary>
    /// Track the duration of an async operation and return its result
    /// </summary>
    Task<T> TrackDurationAsync<T>(string name, Func<Task<T>> func, params string[] labelValues);

    /// <summary>
    /// Get or create a metric family
    /// </summary>
    ICollectorRegistry GetRegistry();
}
