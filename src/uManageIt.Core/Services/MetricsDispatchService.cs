using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using uManageIt.Core.Configuration;
using uManageIt.Core.Contracts;

namespace uManageIt.Core.Services;

public sealed class MetricsDispatchService(
    Channel<MetricEnvelope> channel,
    IHttpClientFactory httpClientFactory,
    IOptions<uManageItOptions> options,
    ILogger<MetricsDispatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var httpClient = httpClientFactory.CreateClient("uManageItMetrics");
        var buffer = new List<MetricEnvelope>(options.Value.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delayTask = Task.Delay(options.Value.FlushInterval, stoppingToken);

                while (buffer.Count < options.Value.BatchSize && channel.Reader.TryRead(out var metric))
                {
                    buffer.Add(metric);
                }

                if (buffer.Count == 0)
                {
                    var first = await channel.Reader.ReadAsync(stoppingToken);
                    buffer.Add(first);
                }

                await delayTask;

                while (buffer.Count < options.Value.BatchSize && channel.Reader.TryRead(out var metric))
                {
                    buffer.Add(metric);
                }

                var request = new MetricsIngestionRequest(options.Value.WebsiteId, buffer.ToArray());
                using var message = new HttpRequestMessage(HttpMethod.Post, options.Value.Endpoint)
                {
                    Content = JsonContent.Create(request)
                };
                message.Headers.TryAddWithoutValidation("X-Api-Key", options.Value.ApiKey);

                var response = await httpClient.SendAsync(message, stoppingToken);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("uManageIt dispatch failed with status code {StatusCode}", response.StatusCode);
                }

                buffer.Clear();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error while dispatching metrics");
                buffer.Clear();
            }
        }
    }
}
