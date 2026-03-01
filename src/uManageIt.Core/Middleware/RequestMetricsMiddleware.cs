using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using uManageIt.Core.Contracts;
using uManageIt.Core.Services;

namespace uManageIt.Core.Middleware;

public sealed class RequestMetricsMiddleware(RequestDelegate next, IMetricSink metricSink)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        await next(context);
        sw.Stop();

        metricSink.TryWrite(new MetricEnvelope(
            Type: "request",
            RecordedAtUtc: DateTimeOffset.UtcNow,
            Data: new Dictionary<string, object?>
            {
                ["method"] = context.Request.Method,
                ["path"] = context.Request.Path.ToString(),
                ["statusCode"] = context.Response.StatusCode,
                ["responseTimeMs"] = sw.Elapsed.TotalMilliseconds,
                ["traceId"] = context.TraceIdentifier
            },
            ResponseTimeMs: sw.Elapsed.TotalMilliseconds,
            Endpoint: context.Request.Path.ToString(),
            StatusCode: context.Response.StatusCode));
    }
}
