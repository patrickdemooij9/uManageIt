using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using uManageIt.Website.Data;
using uManageIt.Website.Domain;
using uManageIt.Website.Services;

namespace uManageIt.Website.Endpoints;

public static class WebsiteEndpoints
{
    public static IEndpointRouteBuilder MapWebsiteEndpoints(this IEndpointRouteBuilder app)
    {
        var websites = app.MapGroup("/api/websites").RequireAuthorization();

        websites.MapPost("/register", RegisterWebsiteAsync);
        websites.MapGet("/mine", GetMyWebsitesAsync);

        var dashboard = app.MapGroup("/api/dashboard").RequireAuthorization();
        dashboard.MapGet("/sites/{websiteId:guid}/summary", GetSummaryAsync);
        dashboard.MapGet("/sites/{websiteId:guid}/metric-types", GetMetricTypesAsync);
        dashboard.MapPost("/sites/{websiteId:guid}/metrics/query", QueryMetricsAsync);

        return app;
    }

    private static async Task<IResult> RegisterWebsiteAsync(
        RegisterWebsiteRequest request,
        ClaimsPrincipal principal,
        ApplicationDbContext dbContext,
        IApiKeyHasher hasher,
        CancellationToken cancellationToken)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Results.Unauthorized();
        }

        var website = new ManagedWebsite
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            BaseUrl = request.BaseUrl,
            OwnerId = parsedUserId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var rawKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var apiKey = new WebsiteApiKey
        {
            Id = Guid.NewGuid(),
            WebsiteId = website.Id,
            KeyHash = hasher.Hash(rawKey),
            KeyPrefix = rawKey[..8],
            CreatedAtUtc = DateTimeOffset.UtcNow,
            IsActive = true
        };

        dbContext.Websites.Add(website);
        dbContext.WebsiteApiKeys.Add(apiKey);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new RegisterWebsiteResponse(website.Id, website.Name, website.BaseUrl, rawKey));
    }

    private static async Task<IResult> GetMyWebsitesAsync(
        ClaimsPrincipal principal,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Results.Unauthorized();
        }

        var websites = await dbContext.Websites
            .Where(x => x.OwnerId == parsedUserId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new WebsiteOverviewResponse(x.Id, x.Name, x.BaseUrl, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(websites);
    }

    [Authorize]
    private static async Task<IResult> GetSummaryAsync(
        Guid websiteId,
        ClaimsPrincipal principal,
        ApplicationDbContext dbContext,
        DashboardQueryService dashboardQuery,
        CancellationToken cancellationToken,
        int hours = 24)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Results.Unauthorized();
        }

        var ownsWebsite = await dbContext.Websites.AnyAsync(x => x.Id == websiteId && x.OwnerId == parsedUserId, cancellationToken);
        if (!ownsWebsite)
        {
            return Results.NotFound();
        }

        var summary = await dashboardQuery.GetWebsiteSummaryAsync(websiteId, TimeSpan.FromHours(Math.Clamp(hours, 1, 168)), cancellationToken);
        return Results.Ok(summary);
    }

    [Authorize]
    private static async Task<IResult> GetMetricTypesAsync(
        Guid websiteId,
        ClaimsPrincipal principal,
        ApplicationDbContext dbContext,
        DashboardQueryService dashboardQuery,
        CancellationToken cancellationToken,
        int hours = 24)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Results.Unauthorized();
        }

        var ownsWebsite = await dbContext.Websites.AnyAsync(x => x.Id == websiteId && x.OwnerId == parsedUserId, cancellationToken);
        if (!ownsWebsite)
        {
            return Results.NotFound();
        }

        var metricTypes = await dashboardQuery.GetMetricTypesAsync(websiteId, TimeSpan.FromHours(Math.Clamp(hours, 1, 24 * 14)), cancellationToken);
        return Results.Ok(metricTypes);
    }

    [Authorize]
    private static async Task<IResult> QueryMetricsAsync(
        Guid websiteId,
        MetricQueryRequest request,
        ClaimsPrincipal principal,
        ApplicationDbContext dbContext,
        DashboardQueryService dashboardQuery,
        CancellationToken cancellationToken)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Results.Unauthorized();
        }

        var ownsWebsite = await dbContext.Websites.AnyAsync(x => x.Id == websiteId && x.OwnerId == parsedUserId, cancellationToken);
        if (!ownsWebsite)
        {
            return Results.NotFound();
        }

        try
        {
            var response = await dashboardQuery.QueryMetricAsync(websiteId, request, cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { ex.Message });
        }
    }
}

public sealed record RegisterWebsiteRequest(string Name, string BaseUrl);
public sealed record RegisterWebsiteResponse(Guid WebsiteId, string Name, string BaseUrl, string ApiKey);
public sealed record WebsiteOverviewResponse(Guid WebsiteId, string Name, string BaseUrl, DateTimeOffset CreatedAtUtc);
