var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

Db.EnsureSchema();

app.MapGet("/health", () => Results.Json(new { status = "ok" }));

app.MapPost("/seed", () => Results.Json(new { status = "seeded", rows = Seeder.Seed() }));

app.MapGet("/report-data", () => Results.Json(ReportQueries.GetReportData()));

app.MapPost("/reports/test", async () =>
{
    var data = ReportQueries.GetReportData();
    var orders = ReportQueries.GetTopOrders(60);
    var html = HtmlReport.Build(data, orders);
    var outPath = Path.Combine(Db.ReportsDir, "test.pdf");
    var method = await PdfRenderer.RenderToPdfAsync(html, outPath);
    return Results.Json(new { file = outPath, method });
});

app.MapPost("/reports", async (HttpRequest req) =>
{
    // Parse the raw body: ReadFromJsonAsync rejects curl -d (it sends a form content-type).
    var force = false;
    using (var reader = new StreamReader(req.Body, System.Text.Encoding.UTF8))
    {
        var raw = await reader.ReadToEndAsync();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(raw);
                force = doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                        && doc.RootElement.TryGetProperty("force", out var f)
                        && f.ValueKind == System.Text.Json.JsonValueKind.True;
            }
            catch { force = false; }
        }
    }

    if (!force)
    {
        var existing = Reports.GetToday();
        if (existing is not null)
            return Results.Json(new { id = existing.Id, file = $"/reports/{existing.Id}/file" });
    }

    var id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];
    await GenerateReportAsync(id);
    return Results.Created($"/reports/{id}", new { id, file = $"/reports/{id}/file" });
});

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

static async Task<string> GenerateReportAsync(string id)
{
    var data = ReportQueries.GetReportData();
    var orders = ReportQueries.GetTopOrders(60);
    var html = HtmlReport.Build(data, orders);
    var outPath = Path.Combine(Db.ReportsDir, $"{id}.pdf");
    await PdfRenderer.RenderToPdfAsync(html, outPath);
    Reports.Insert(id, outPath, DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss"));
    return outPath;
}
