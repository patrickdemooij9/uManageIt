using uManageIt.Core.Contracts;

namespace uManageIt.Core.Services;

public interface IMetricSink
{
    bool TryWrite(MetricEnvelope metric);
}
