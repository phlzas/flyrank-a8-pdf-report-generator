var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ReportQueue>();
builder.Services.AddSingleton<MailSender>();
builder.Services.AddSingleton<ReportGenerator>();
builder.Services.AddHostedService<ReportWorker>();
builder.Services.AddHostedService<WeeklyReportJob>();

var app = builder.Build();

Db.EnsureSchema();

app.MapGet("/health", () => Results.Json(new { status = "ok" }));

app.MapPost("/seed", () => Results.Json(new { status = "seeded", rows = Seeder.Seed() }));

app.MapGet("/report-data", (HttpRequest req) =>
    Results.Json(ReportQueries.GetReportData(QueryDays(req) ?? ReportQueries.DefaultDays)));

app.MapPost("/reports/test", async () =>
{
    var data = ReportQueries.GetReportData();
    var orders = ReportQueries.GetTopOrders(60);
    var html = HtmlReport.Build(data, orders);
    var outPath = Path.Combine(Db.ReportsDir, "test.pdf");
    var method = await PdfRenderer.RenderToPdfAsync(html, outPath);
    return Results.Json(new { file = outPath, method });
});

app.MapPost("/reports", async (HttpRequest req, ReportGenerator generator) =>
{
    // Parse the raw body: ReadFromJsonAsync rejects curl -d (it sends a form content-type).
    var force = false;
    int? bodyDays = null;
    using (var reader = new StreamReader(req.Body, System.Text.Encoding.UTF8))
    {
        var raw = await reader.ReadToEndAsync();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    force = doc.RootElement.TryGetProperty("force", out var f)
                            && f.ValueKind == System.Text.Json.JsonValueKind.True;
                    if (doc.RootElement.TryGetProperty("days", out var d)
                        && d.ValueKind == System.Text.Json.JsonValueKind.Number
                        && d.TryGetInt32(out var parsed))
                        bodyDays = parsed;
                }
            }
            catch { force = false; }
        }
    }

    var days = ReportQueries.ClampDays(bodyDays ?? QueryDays(req) ?? ReportQueries.DefaultDays);

    if (!force)
    {
        var existing = Reports.GetToday();
        if (existing is not null)
            return Results.Json(new { id = existing.Id, file = $"/reports/{existing.Id}/file" });
    }

    var id = ReportGenerator.NewId();
    await generator.GenerateAsync(id, days);
    return Results.Created($"/reports/{id}", new { id, file = $"/reports/{id}/file" });
});

app.MapPost("/reports/background", async (HttpRequest req, ReportQueue queue) =>
{
    var days = ReportQueries.ClampDays(QueryDays(req) ?? ReportQueries.DefaultDays);
    var id = ReportGenerator.NewId();
    Reports.Insert(id, ReportGenerator.PathFor(id),
        DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss"), "pending", days);
    await queue.EnqueueAsync(id);
    return Results.Json(new { id, status = "pending", file = $"/reports/{id}/file" }, statusCode: 202);
});

app.MapGet("/reports", () =>
    Results.Json(Reports.List().Select(r => new
    {
        id = r.Id,
        createdAt = r.CreatedAt,
        status = r.Status,
        file = $"/reports/{r.Id}/file"
    })));

app.MapGet("/reports/{id}", (string id) =>
{
    var rec = Reports.Get(id);
    return rec is null ? Results.NotFound() : Results.Json(rec);
});

app.MapGet("/reports/{id}/file", (string id) =>
{
    var rec = Reports.Get(id);
    if (rec is null || !File.Exists(rec.Path)) return Results.NotFound();
    return Results.File(rec.Path, "application/pdf");
});

app.Run();

static int? QueryDays(HttpRequest req)
{
    var raw = req.Query["days"].ToString();
    return int.TryParse(raw, out var value) ? value : null;
}
