using Npgsql;

namespace uManageIt.Website.Services;

public sealed record WebsiteSummaryResponse(
    bool IsOnline,
    double? AverageResponseTimeMs,
    double? AverageCpuUsagePercent,
    double? AverageMemoryUsedMb,
    int RequestsLast24Hours,
    IReadOnlyCollection<TimeBucketPoint> RequestRate);

public sealed record TimeBucketPoint(DateTimeOffset Bucket, int RequestCount, double? AvgResponseTimeMs);

public sealed class DashboardQueryService(NpgsqlDataSource dataSource)
{
    public async Task<WebsiteSummaryResponse> GetWebsiteSummaryAsync(Guid websiteId, TimeSpan period, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var from = now.Subtract(period);

        const string aggregateSql = """
            SELECT
                AVG(response_time_ms) FILTER (WHERE metric_type = 'request') AS avg_response_time,
                AVG(cpu_usage_percent) FILTER (WHERE metric_type = 'runtime') AS avg_cpu,
                AVG(memory_used_mb) FILTER (WHERE metric_type = 'runtime') AS avg_memory,
                COUNT(*) FILTER (WHERE metric_type = 'request') AS request_count,
                MAX(recorded_at) AS last_seen
            FROM metrics
            WHERE website_id = @website_id
              AND recorded_at >= @from;
            """;

        double? avgResponse = null;
        double? avgCpu = null;
        double? avgMemory = null;
        var requestCount = 0;
        DateTimeOffset? lastSeen = null;

        await using (var cmd = new NpgsqlCommand(aggregateSql, connection))
        {
            cmd.Parameters.AddWithValue("website_id", websiteId);
            cmd.Parameters.AddWithValue("from", from);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                avgResponse = reader.IsDBNull(0) ? null : reader.GetDouble(0);
                avgCpu = reader.IsDBNull(1) ? null : reader.GetDouble(1);
                avgMemory = reader.IsDBNull(2) ? null : reader.GetDouble(2);
                requestCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                lastSeen = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4);
            }
        }

        const string bucketsSql = """
            SELECT
                time_bucket(INTERVAL '15 minutes', recorded_at) AS bucket,
                COUNT(*) AS request_count,
                AVG(response_time_ms) AS avg_response_time
            FROM metrics
            WHERE website_id = @website_id
              AND metric_type = 'request'
              AND recorded_at >= @from
            GROUP BY bucket
            ORDER BY bucket;
            """;

        var points = new List<TimeBucketPoint>();
        await using (var cmd = new NpgsqlCommand(bucketsSql, connection))
        {
            cmd.Parameters.AddWithValue("website_id", websiteId);
            cmd.Parameters.AddWithValue("from", from);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                points.Add(new TimeBucketPoint(
                    reader.GetFieldValue<DateTimeOffset>(0),
                    reader.GetInt32(1),
                    reader.IsDBNull(2) ? null : reader.GetDouble(2)));
            }
        }

        var isOnline = lastSeen.HasValue && lastSeen.Value >= now.Subtract(TimeSpan.FromMinutes(2));
        return new WebsiteSummaryResponse(isOnline, avgResponse, avgCpu, avgMemory, requestCount, points);
    }

    public async Task<IReadOnlyCollection<string>> GetMetricTypesAsync(Guid websiteId, TimeSpan period, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = """
            SELECT DISTINCT metric_type
            FROM metrics
            WHERE website_id = @website_id
              AND recorded_at >= @from
            ORDER BY metric_type;
            """;

        var result = new List<string>();
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("website_id", websiteId);
        cmd.Parameters.AddWithValue("from", DateTimeOffset.UtcNow.Subtract(period));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    public async Task<MetricQueryResponse> QueryMetricAsync(Guid websiteId, MetricQueryRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var hours = Math.Clamp(request.Hours, 1, 24 * 14);
        var bucketMinutes = Math.Clamp(request.BucketMinutes, 1, 240);
        var from = now.Subtract(TimeSpan.FromHours(hours));

        var metricType = request.MetricType.Trim();
        if (string.IsNullOrWhiteSpace(metricType))
        {
            throw new ArgumentException("Metric type is required.", nameof(request));
        }

        if (request.Aggregation is not MetricAggregation.Count && string.IsNullOrWhiteSpace(request.NumericField))
        {
            throw new ArgumentException("NumericField is required for aggregations other than Count.", nameof(request));
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var overallSql = BuildOverallSql(request.Aggregation);
        await using var overallCmd = new NpgsqlCommand(overallSql, connection);
        overallCmd.Parameters.AddWithValue("website_id", websiteId);
        overallCmd.Parameters.AddWithValue("metric_type", metricType);
        overallCmd.Parameters.AddWithValue("from", from);
        overallCmd.Parameters.AddWithValue("numeric_field", request.NumericField ?? string.Empty);

        var overallRaw = await overallCmd.ExecuteScalarAsync(cancellationToken);
        var overall = overallRaw is null or DBNull ? 0d : Convert.ToDouble(overallRaw);

        var bucketsSql = BuildBucketSql(request.Aggregation, bucketMinutes);
        await using var bucketsCmd = new NpgsqlCommand(bucketsSql, connection);
        bucketsCmd.Parameters.AddWithValue("website_id", websiteId);
        bucketsCmd.Parameters.AddWithValue("metric_type", metricType);
        bucketsCmd.Parameters.AddWithValue("from", from);
        bucketsCmd.Parameters.AddWithValue("numeric_field", request.NumericField ?? string.Empty);

        var points = new List<MetricPoint>();
        await using var reader = await bucketsCmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            points.Add(new MetricPoint(reader.GetFieldValue<DateTimeOffset>(0), reader.GetDouble(1)));
        }

        return new MetricQueryResponse(websiteId, metricType, request.Aggregation, request.NumericField, overall, points);
    }

    private static string BuildOverallSql(MetricAggregation aggregation)
    {
        var expression = BuildAggregationExpression(aggregation);
        return $"""
            SELECT COALESCE({expression}, 0)
            FROM metrics
            WHERE website_id = @website_id
              AND metric_type = @metric_type
              AND recorded_at >= @from;
            """;
    }

    private static string BuildBucketSql(MetricAggregation aggregation, int bucketMinutes)
    {
        var expression = BuildAggregationExpression(aggregation);
        return $"""
            SELECT time_bucket(INTERVAL '{bucketMinutes} minutes', recorded_at) AS bucket,
                   COALESCE({expression}, 0) AS value
            FROM metrics
            WHERE website_id = @website_id
              AND metric_type = @metric_type
              AND recorded_at >= @from
            GROUP BY bucket
            ORDER BY bucket;
            """;
    }

    private static string BuildAggregationExpression(MetricAggregation aggregation)
        => aggregation switch
        {
            MetricAggregation.Count => "COUNT(*)::double precision",
            MetricAggregation.Average => "AVG(CASE WHEN (payload ->> @numeric_field) ~ '^-?[0-9]+(\\.[0-9]+)?$' THEN (payload ->> @numeric_field)::double precision END)",
            MetricAggregation.Min => "MIN(CASE WHEN (payload ->> @numeric_field) ~ '^-?[0-9]+(\\.[0-9]+)?$' THEN (payload ->> @numeric_field)::double precision END)",
            MetricAggregation.Max => "MAX(CASE WHEN (payload ->> @numeric_field) ~ '^-?[0-9]+(\\.[0-9]+)?$' THEN (payload ->> @numeric_field)::double precision END)",
            MetricAggregation.Sum => "SUM(CASE WHEN (payload ->> @numeric_field) ~ '^-?[0-9]+(\\.[0-9]+)?$' THEN (payload ->> @numeric_field)::double precision END)",
            _ => throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Unsupported aggregation")
        };
}
