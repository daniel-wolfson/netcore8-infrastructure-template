using Microsoft.Extensions.Options;
using Prometheus;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Custom.Framework.Monitoring.Prometheus;

/// <summary>
/// Implementation of Prometheus metrics manager
/// Thread-safe singleton that manages metric creation and recording
/// </summary>
public class PrometheusMetricsService : IPrometheusMetricsService
{
    private readonly PrometheusOptions _options;
    private readonly ICollectorRegistry _registry;
    private readonly ConcurrentDictionary<string, Counter> _counters = new();
    private readonly ConcurrentDictionary<string, Gauge> _gauges = new();
    private readonly ConcurrentDictionary<string, Histogram> _histograms = new();
    private readonly ConcurrentDictionary<string, Summary> _summaries = new();

    public PrometheusMetricsService(IOptions<PrometheusOptions> options)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _registry = Metrics.DefaultRegistry;
    }

    public Counter CreateCounter(string name, string help, params string[] labelNames)
    {
        var key = GetMetricKey(name, labelNames);
        return _counters.GetOrAdd(key, _ =>
        {
            var metricName = NormalizeMetricName(name);
            var counter = Metrics.CreateCounter(metricName, help, 
                new CounterConfiguration
                {
                    LabelNames = CombineLabels(labelNames)
                });
            return counter;
        });
    }

    public Gauge CreateGauge(string name, string help, params string[] labelNames)
    {
        var key = GetMetricKey(name, labelNames);
        return _gauges.GetOrAdd(key, _ =>
        {
            var metricName = NormalizeMetricName(name);
            return Metrics.CreateGauge(metricName, help,
                new GaugeConfiguration
                {
                    LabelNames = CombineLabels(labelNames)
                });
        });
    }

    public Histogram CreateHistogram(string name, string help, double[]? buckets = null, params string[] labelNames)
    {
        var key = GetMetricKey(name, labelNames);
        return _histograms.GetOrAdd(key, _ =>
        {
            var metricName = NormalizeMetricName(name);
            return Metrics.CreateHistogram(metricName, help,
                new HistogramConfiguration
                {
                    LabelNames = CombineLabels(labelNames),
                    Buckets = buckets ?? _options.HistogramBuckets
                });
        });
    }

    public Summary CreateSummary(string name, string help, params string[] labelNames)
    {
        var key = GetMetricKey(name, labelNames);
        return _summaries.GetOrAdd(key, _ =>
        {
            var metricName = NormalizeMetricName(name);
            return Metrics.CreateSummary(metricName, help,
                new SummaryConfiguration
                {
                    LabelNames = CombineLabels(labelNames),
                    Objectives = new[]
                    {
                        new QuantileEpsilonPair(0.5, 0.05),   // 50th percentile ±5%
                        new QuantileEpsilonPair(0.9, 0.01),   // 90th percentile ±1%
                        new QuantileEpsilonPair(0.95, 0.005), // 95th percentile ±0.5%
                        new QuantileEpsilonPair(0.99, 0.001)  // 99th percentile ±0.1%
                    }
                });
        });
    }

    public void IncrementCounter(string name, params string[] labelValues)
    {
        IncrementCounter(name, 1, labelValues);
    }

    public void IncrementCounter(string name, double value, params string[] labelValues)
    {
        try
        {
            var counter = GetOrCreateCounter(name);
            if (labelValues.Length > 0)
            {
                counter.WithLabels(CombineLabelValues(labelValues)).Inc(value);
            }
            else
            {
                counter.Inc(value);
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - metrics shouldn't break application
            Console.WriteLine($"Error incrementing counter '{name}': {ex.Message}");
        }
    }

    public void SetGauge(string name, double value, params string[] labelValues)
    {
        try
        {
            var gauge = GetOrCreateGauge(name);
            if (labelValues.Length > 0)
            {
                gauge.WithLabels(CombineLabelValues(labelValues)).Set(value);
            }
            else
            {
                gauge.Set(value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting gauge '{name}': {ex.Message}");
        }
    }

    public void IncrementGauge(string name, double value = 1, params string[] labelValues)
    {
        try
        {
            var gauge = GetOrCreateGauge(name);
            if (labelValues.Length > 0)
            {
                gauge.WithLabels(CombineLabelValues(labelValues)).Inc(value);
            }
            else
            {
                gauge.Inc(value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error incrementing gauge '{name}': {ex.Message}");
        }
    }

    public void DecrementGauge(string name, double value = 1, params string[] labelValues)
    {
        try
        {
            var gauge = GetOrCreateGauge(name);
            if (labelValues.Length > 0)
            {
                gauge.WithLabels(CombineLabelValues(labelValues)).Dec(value);
            }
            else
            {
                gauge.Dec(value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error decrementing gauge '{name}': {ex.Message}");
        }
    }

    public void ObserveHistogram(string name, double value, params string[] labelValues)
    {
        try
        {
            var histogram = GetOrCreateHistogram(name);
            if (labelValues.Length > 0)
            {
                histogram.WithLabels(CombineLabelValues(labelValues)).Observe(value);
            }
            else
            {
                histogram.Observe(value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error observing histogram '{name}': {ex.Message}");
        }
    }

    public void ObserveSummary(string name, double value, params string[] labelValues)
    {
        try
        {
            var summary = GetOrCreateSummary(name);
            if (labelValues.Length > 0)
            {
                summary.WithLabels(CombineLabelValues(labelValues)).Observe(value);
            }
            else
            {
                summary.Observe(value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error observing summary '{name}': {ex.Message}");
        }
    }

    public void TrackDuration(string name, Action action, params string[] labelValues)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            action();
        }
        finally
        {
            stopwatch.Stop();
            ObserveHistogram(name, stopwatch.Elapsed.TotalSeconds, labelValues);
        }
    }

    public async Task TrackDurationAsync(string name, Func<Task> action, params string[] labelValues)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await action();
        }
        finally
        {
            stopwatch.Stop();
            ObserveHistogram(name, stopwatch.Elapsed.TotalSeconds, labelValues);
        }
    }

    public T TrackDuration<T>(string name, Func<T> func, params string[] labelValues)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return func();
        }
        finally
        {
            stopwatch.Stop();
            ObserveHistogram(name, stopwatch.Elapsed.TotalSeconds, labelValues);
        }
    }

    public async Task<T> TrackDurationAsync<T>(string name, Func<Task<T>> func, params string[] labelValues)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await func();
        }
        finally
        {
            stopwatch.Stop();
            ObserveHistogram(name, stopwatch.Elapsed.TotalSeconds, labelValues);
        }
    }

    public ICollectorRegistry GetRegistry()
    {
        return _registry;
    }

    #region Private Helper Methods

    private Counter GetOrCreateCounter(string name)
    {
        var key = GetMetricKey(name, Array.Empty<string>());
        return _counters.GetOrAdd(key, _ => CreateCounter(name, $"Counter: {name}"));
    }

    private Gauge GetOrCreateGauge(string name)
    {
        var key = GetMetricKey(name, Array.Empty<string>());
        return _gauges.GetOrAdd(key, _ => CreateGauge(name, $"Gauge: {name}"));
    }

    private Histogram GetOrCreateHistogram(string name)
    {
        var key = GetMetricKey(name, Array.Empty<string>());
        return _histograms.GetOrAdd(key, _ => CreateHistogram(name, $"Histogram: {name}"));
    }

    private Summary GetOrCreateSummary(string name)
    {
        var key = GetMetricKey(name, Array.Empty<string>());
        return _summaries.GetOrAdd(key, _ => CreateSummary(name, $"Summary: {name}"));
    }

    private static string GetMetricKey(string name, string[] labelNames)
    {
        return $"{name}_{string.Join("_", labelNames)}";
    }

    private static string NormalizeMetricName(string name)
    {
        // Prometheus metric names must match [a-zA-Z_:][a-zA-Z0-9_:]*
        return name.Replace("-", "_").Replace(".", "_").Replace(" ", "_");
    }

    private string[] CombineLabels(string[] customLabels)
    {
        // Add application-level labels
        var allLabels = new List<string>();
        
        if (_options.CustomLabels.Count > 0)
        {
            allLabels.AddRange(_options.CustomLabels.Keys);
        }
        
        allLabels.AddRange(customLabels);
        
        return allLabels.ToArray();
    }

    private string[] CombineLabelValues(string[] customValues)
    {
        // Add application-level label values
        var allValues = new List<string>();
        
        if (_options.CustomLabels.Count > 0)
        {
            allValues.AddRange(_options.CustomLabels.Values);
        }
        
        allValues.AddRange(customValues);
        
        return allValues.ToArray();
    }

    #endregion
}
