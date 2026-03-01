using uManageIt.Core.Contracts;

namespace uManageIt.Core.Services;

public interface IuManageItTelemetryClient
{
    void TrackMetric(string metricType, Dictionary<string, object?> payload, DateTimeOffset? recordedAtUtc = null);
    void TrackEvent(string eventName, Dictionary<string, object?>? payload = null, DateTimeOffset? occurredAtUtc = null);
}

public interface IuManageItEventTracker
{
    void TrackEvent(string eventName, Dictionary<string, object?>? payload = null);
}

public sealed class uManageItEventTracker(IMetricSink metricSink) : IuManageItEventTracker, IuManageItTelemetryClient
{
    public void TrackMetric(string metricType, Dictionary<string, object?> payload, DateTimeOffset? recordedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(metricType))
        {
            throw new ArgumentException("Metric type is required.", nameof(metricType));
        }

        metricSink.TryWrite(new MetricEnvelope(
            Type: metricType,
            RecordedAtUtc: recordedAtUtc ?? DateTimeOffset.UtcNow,
            Data: payload));
    }

    public void TrackEvent(string eventName, Dictionary<string, object?>? payload = null)
        => TrackEvent(eventName, payload, null);

    public void TrackEvent(string eventName, Dictionary<string, object?>? payload = null, DateTimeOffset? occurredAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentException("Event name is required.", nameof(eventName));
        }

        var data = payload ?? new Dictionary<string, object?>();
        data["eventName"] = eventName;
        data["category"] ??= "application";

        TrackMetric("event", data, occurredAtUtc);
    }
}
