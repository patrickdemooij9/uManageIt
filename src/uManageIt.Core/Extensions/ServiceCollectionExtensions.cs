using Microsoft.AspNetCore.Builder;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using uManageIt.Core.Configuration;
using uManageIt.Core.Contracts;
using uManageIt.Core.Middleware;
using uManageIt.Core.Services;

namespace uManageIt.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AdduManageItCore(this IServiceCollection services, Action<uManageItOptions> configure)
    {
        services.Configure(configure);

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<uManageItOptions>>().Value;
            return Channel.CreateBounded<MetricEnvelope>(new BoundedChannelOptions(options.MaxQueueSize)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });
        });

        services.AddHttpClient("uManageItMetrics");

        services.AddSingleton<IMetricSink, ChannelMetricSink>();
        services.AddSingleton<uManageItEventTracker>();
        services.AddSingleton<IuManageItEventTracker>(sp => sp.GetRequiredService<uManageItEventTracker>());
        services.AddSingleton<IuManageItTelemetryClient>(sp => sp.GetRequiredService<uManageItEventTracker>());
        services.AddHostedService<RuntimeMetricsCollector>();
        services.AddHostedService<MetricsDispatchService>();

        return services;
    }

    public static IApplicationBuilder UseuManageItRequestMetrics(this IApplicationBuilder app)
        => app.UseMiddleware<RequestMetricsMiddleware>();
}
