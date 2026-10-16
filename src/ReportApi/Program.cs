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

app.Run();
