namespace uManageIt.Core.Contracts;

public sealed record MetricsIngestionRequest(Guid WebsiteId, IReadOnlyCollection<MetricEnvelope> Metrics);

public sealed record MetricEnvelope(
    string Type,
    DateTimeOffset RecordedAtUtc,
    Dictionary<string, object?> Data,
    double? ResponseTimeMs = null,
    string? Endpoint = null,
    int? StatusCode = null,
    double? CpuUsagePercent = null,
    double? MemoryUsedMb = null,
    int? ThreadsCount = null);
