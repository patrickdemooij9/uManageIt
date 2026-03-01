using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using uManageIt.Core.Configuration;
using uManageIt.Core.Contracts;

namespace uManageIt.Core.Services;

public sealed class RuntimeMetricsCollector(
    IMetricSink metricSink,
    IOptions<uManageItOptions> options,
    ILogger<RuntimeMetricsCollector> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var process = Process.GetCurrentProcess();
        var previousCpu = process.TotalProcessorTime;
        var previousTime = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(options.Value.RuntimeCollectionInterval, stoppingToken);

                process.Refresh();
                var now = DateTimeOffset.UtcNow;
                var totalCpu = process.TotalProcessorTime;
                var cpuDelta = (totalCpu - previousCpu).TotalMilliseconds;
                var timeDelta = (now - previousTime).TotalMilliseconds;

                var cpuUsage = timeDelta <= 0
                    ? 0
                    : (cpuDelta / (Environment.ProcessorCount * timeDelta)) * 100;

                previousCpu = totalCpu;
                previousTime = now;

                var workingSetMb = process.WorkingSet64 / 1024d / 1024d;
                var totalMemoryMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024d / 1024d;
                var threads = process.Threads.Count;

                metricSink.TryWrite(new MetricEnvelope(
                    Type: "runtime",
                    RecordedAtUtc: now,
                    Data: new Dictionary<string, object?>
                    {
                        ["cpuUsagePercent"] = cpuUsage,
                        ["memoryUsedMb"] = workingSetMb,
                        ["totalMemoryMb"] = totalMemoryMb,
                        ["threads"] = threads,
                        ["gcHeapSizeBytes"] = GC.GetTotalMemory(false)
                    },
                    CpuUsagePercent: cpuUsage,
                    MemoryUsedMb: workingSetMb,
                    ThreadsCount: threads));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to collect runtime metric");
            }
        }
    }
}
