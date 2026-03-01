using System.Text.Json;
using Npgsql;

namespace uManageIt.Website.Services;

public sealed class MetricsIngestionService(NpgsqlDataSource dataSource)
{
    public async Task<int> WriteBatchAsync(Guid websiteId, IReadOnlyCollection<MetricEnvelope> metrics, CancellationToken cancellationToken)
    {
        if (metrics.Count == 0)
        {
            return 0;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sql = """
            INSERT INTO metrics (
                recorded_at, website_id, metric_type, payload,
                response_time_ms, endpoint, status_code,
                cpu_usage_percent, memory_used_mb, threads_count)
            VALUES (
                @recorded_at, @website_id, @metric_type, @payload::jsonb,
                @response_time_ms, @endpoint, @status_code,
                @cpu_usage_percent, @memory_used_mb, @threads_count);
            """;

        var written = 0;

        foreach (var metric in metrics)
        {
            await using var cmd = new NpgsqlCommand(sql, connection, transaction);
            cmd.Parameters.AddWithValue("recorded_at", metric.RecordedAtUtc);
            cmd.Parameters.AddWithValue("website_id", websiteId);
            cmd.Parameters.AddWithValue("metric_type", metric.Type);
            cmd.Parameters.AddWithValue("payload", JsonSerializer.Serialize(metric.Data));
            cmd.Parameters.AddWithValue("response_time_ms", (object?)metric.ResponseTimeMs ?? DBNull.Value);
            cmd.Parameters.AddWithValue("endpoint", (object?)metric.Endpoint ?? DBNull.Value);
            cmd.Parameters.AddWithValue("status_code", (object?)metric.StatusCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cpu_usage_percent", (object?)metric.CpuUsagePercent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("memory_used_mb", (object?)metric.MemoryUsedMb ?? DBNull.Value);
            cmd.Parameters.AddWithValue("threads_count", (object?)metric.ThreadsCount ?? DBNull.Value);

            written += await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return written;
    }
}
