namespace uManageIt.Website.Services;

public enum MetricAggregation
{
    Count,
    Average,
    Min,
    Max,
    Sum
}

public sealed record MetricQueryRequest(
    string MetricType,
    MetricAggregation Aggregation,
    string? NumericField,
    int Hours = 24,
    int BucketMinutes = 15);

public sealed record MetricPoint(DateTimeOffset Bucket, double Value);

public sealed record MetricQueryResponse(
    Guid WebsiteId,
    string MetricType,
    MetricAggregation Aggregation,
    string? NumericField,
    double OverallValue,
    IReadOnlyCollection<MetricPoint> Points);
