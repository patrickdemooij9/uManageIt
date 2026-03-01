using Microsoft.EntityFrameworkCore;
using uManageIt.Website.Data;
using uManageIt.Website.Services;

namespace uManageIt.Website.Endpoints;

public static class IngestionEndpoints
{
    private const string ApiKeyHeader = "X-Api-Key";

    public static IEndpointRouteBuilder MapIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        var ingestion = app.MapGroup("/api/ingest");
        ingestion.MapPost("/metrics", IngestMetricsAsync);
        return app;
    }

    private static async Task<IResult> IngestMetricsAsync(
        HttpContext httpContext,
        MetricsIngestionRequest request,
        ApplicationDbContext dbContext,
        IApiKeyHasher hasher,
        MetricsIngestionService ingestionService,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Headers.TryGetValue(ApiKeyHeader, out var rawApiKey))
        {
            return Results.Unauthorized();
        }

        var apiKeyHash = hasher.Hash(rawApiKey.ToString());
        var validKey = await dbContext.WebsiteApiKeys
            .AsNoTracking()
            .AnyAsync(x => x.WebsiteId == request.WebsiteId && x.KeyHash == apiKeyHash && x.IsActive, cancellationToken);

        if (!validKey)
        {
            return Results.Unauthorized();
        }

        var records = request.Metrics.Where(x => x.RecordedAtUtc > DateTimeOffset.UnixEpoch && !string.IsNullOrWhiteSpace(x.Type)).ToArray();
        if (records.Length == 0)
        {
            return Results.BadRequest("Metrics payload is empty.");
        }

        await ingestionService.WriteBatchAsync(request.WebsiteId, records, cancellationToken);
        return Results.Accepted();
    }
}
