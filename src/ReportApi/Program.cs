var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

Db.EnsureSchema();

app.MapGet("/health", () => Results.Json(new { status = "ok" }));

app.MapPost("/seed", () => Results.Json(new { status = "seeded", rows = Seeder.Seed() }));

app.MapGet("/report-data", () => Results.Json(ReportQueries.GetReportData()));

app.Run();
