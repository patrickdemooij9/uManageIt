using Npgsql;

namespace uManageIt.Website.Services;

public sealed class TimescaleInitializer(NpgsqlDataSource dataSource, ILogger<TimescaleInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = """
            CREATE EXTENSION IF NOT EXISTS timescaledb;

            CREATE TABLE IF NOT EXISTS metrics (
                recorded_at TIMESTAMPTZ NOT NULL,
                website_id UUID NOT NULL,
                metric_type TEXT NOT NULL,
                payload JSONB NOT NULL,
                response_time_ms DOUBLE PRECISION NULL,
                endpoint TEXT NULL,
                status_code INTEGER NULL,
                cpu_usage_percent DOUBLE PRECISION NULL,
                memory_used_mb DOUBLE PRECISION NULL,
                threads_count INTEGER NULL
            );

            SELECT create_hypertable('metrics', by_range('recorded_at'), if_not_exists => TRUE);

            CREATE INDEX IF NOT EXISTS idx_metrics_website_time ON metrics (website_id, recorded_at DESC);
            CREATE INDEX IF NOT EXISTS idx_metrics_type_time ON metrics (metric_type, recorded_at DESC);

            SELECT add_retention_policy('metrics', INTERVAL '90 days', if_not_exists => TRUE);
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        logger.LogInformation("TimescaleDB initialized");
    }
}
