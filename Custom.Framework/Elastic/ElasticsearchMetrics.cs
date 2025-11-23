using System.Diagnostics.Metrics;

namespace Custom.Framework.Elastic;

/// <summary>
/// OpenTelemetry metrics for Elasticsearch operations
/// </summary>
public class ElasticsearchMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _documentsIndexed;
    private readonly Counter<long> _indexingErrors;
    private readonly Histogram<double> _indexingDuration;
    private readonly Counter<long> _searchRequests;
    private readonly Histogram<double> _searchDuration;

    public ElasticsearchMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("Custom.Framework.Elasticsearch");

        _documentsIndexed = _meter.CreateCounter<long>(
            "elasticsearch.documents.indexed",
            unit: "documents",
            description: "Number of documents successfully indexed");

        _indexingErrors = _meter.CreateCounter<long>(
            "elasticsearch.indexing.errors",
            unit: "errors",
            description: "Number of indexing errors");

        _indexingDuration = _meter.CreateHistogram<double>(
            "elasticsearch.indexing.duration",
            unit: "ms",
            description: "Duration of indexing operations");

        _searchRequests = _meter.CreateCounter<long>(
            "elasticsearch.search.requests",
            unit: "requests",
            description: "Number of search requests");

        _searchDuration = _meter.CreateHistogram<double>(
            "elasticsearch.search.duration",
            unit: "ms",
            description: "Duration of search operations");
    }

    public void RecordDocumentsIndexed(long count, string index)
    {
        _documentsIndexed.Add(count, new KeyValuePair<string, object?>("index", index));
    }

    public void RecordIndexingError(string index, string errorType)
    {
        _indexingErrors.Add(1,
            new KeyValuePair<string, object?>("index", index),
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    public void RecordIndexingDuration(double durationMs, string index)
    {
        _indexingDuration.Record(durationMs,
            new KeyValuePair<string, object?>("index", index));
    }

    public void RecordSearchRequest(string index)
    {
        _searchRequests.Add(1,
            new KeyValuePair<string, object?>("index", index));
    }

    public void RecordSearchDuration(double durationMs, string index)
    {
        _searchDuration.Record(durationMs,
            new KeyValuePair<string, object?>("index", index));
    }
}
