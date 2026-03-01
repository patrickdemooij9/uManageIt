# uManageIt architecture

## Overview

- `uManageIt.Core` (NuGet package): captures runtime + request + custom event metrics using a bounded in-memory channel and background dispatch to avoid request-path blocking.
  - Includes a generic telemetry API so developers can push their own metric types and payloads.
- `uManageIt.Website` (dashboard/backend):
  - ASP.NET Identity authentication (`/account/register`, `/account/login`).
  - Website registration + API key generation per website.
  - Ingestion endpoint protected by API key.
  - TimescaleDB hypertable for time-series metrics with 90-day retention.
- Polling dashboard summary endpoint for chart-friendly aggregates.
  - Generic metric query endpoints for charting any metric type with aggregations.

## TimescaleDB schema

Table `metrics` is a hypertable partitioned by `recorded_at`.

Core columns:
- `recorded_at`
- `website_id`
- `metric_type` (`runtime`, `request`, `event`, ...)
- `payload` (`jsonb`, extensibility point)

Fast-query columns for common dashboards:
- `response_time_ms`
- `endpoint`
- `status_code`
- `cpu_usage_percent`
- `memory_used_mb`
- `threads_count`

Indexes:
- `(website_id, recorded_at desc)`
- `(metric_type, recorded_at desc)`

Retention policy: 90 days.

## Ingestion contract

`POST /api/ingest/metrics` with `X-Api-Key` header.

Body:

```json
{
  "websiteId": "GUID",
  "metrics": [
    {
      "type": "request",
      "recordedAtUtc": "2026-01-01T00:00:00Z",
      "data": {
        "path": "/api/content",
        "method": "GET"
      },
      "responseTimeMs": 23.2,
      "endpoint": "/api/content",
      "statusCode": 200
    }
  ]
}
```

## Polling API

- `GET /api/websites/mine`
- `POST /api/websites/register`
- `GET /api/dashboard/sites/{websiteId}/summary?hours=24`
- `GET /api/dashboard/sites/{websiteId}/metric-types?hours=24`
- `POST /api/dashboard/sites/{websiteId}/metrics/query`

Example generic query body:

```json
{
  "metricType": "event",
  "aggregation": "Count",
  "numericField": null,
  "hours": 24,
  "bucketMinutes": 15
}
```

You can query custom developer-defined metric types the same way, and for numeric payload keys use aggregations like `Average`, `Min`, `Max`, and `Sum`.

The summary API returns:
- online/offline status (based on recent heartbeat metrics)
- average response time
- average CPU + memory
- request count
- 15-minute request buckets for charting
