using System.Threading.Channels;
using uManageIt.Core.Contracts;

namespace uManageIt.Core.Services;

public sealed class ChannelMetricSink(Channel<MetricEnvelope> channel) : IMetricSink
{
    public bool TryWrite(MetricEnvelope metric) => channel.Writer.TryWrite(metric);
}
