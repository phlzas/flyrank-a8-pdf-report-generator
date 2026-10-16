# FlyRank A8 - PDF report generator

A small .NET 9 minimal-API service that reads orders from SQLite, aggregates them
with SQL, renders an HTML report, converts it to a real PDF with a headless
browser, stores the PDF on disk, and returns a **link** to it (never the bytes).

## Run

```
dotnet run --project src/ReportApi --urls http://localhost:5188
```

## Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| GET  | `/health` | Liveness check, returns `{"status":"ok"}` |
| POST | `/seed` | Reseeds exactly 200 random orders (idempotent) |
| GET  | `/report-data` | The four aggregation result sets as JSON |
| POST | `/reports` | Runs the full pipeline, returns `201` with `{id, file}` |
| GET  | `/reports/{id}` | The stored report record as JSON (404 if unknown) |
| GET  | `/reports/{id}/file` | The PDF bytes from disk (`application/pdf`) |

## When to move generation out of the request

You would move generation into a background job once PDF rendering is slow or
bursty enough that holding the HTTP request open (and a worker thread) for the
whole render hurts latency or throughput - e.g. large reports, many concurrent
requests, or retries - so the request can return `202 Accepted` with a status
URL and the job writes the PDF and flips the record to "ready" when done.
