using Microsoft.Data.Sqlite;
using Microsoft.Playwright;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var dbPath = "report.db";
var connectionString = $"Data Source={dbPath}";
var reportsDir = "reports";
Directory.CreateDirectory(reportsDir);

InitializeDatabase();
SeedDatabase();

app.MapGet("/health", () => Results.Json(new { status = "ok" }));

app.MapPost("/seed", () =>
{
    SeedDatabase();
    return Results.Ok(new { status = "seeded", count = GetOrdersCount() });
});

app.MapPost("/reports", async (HttpContext ctx) =>
{
    var force = false;
    try
    {
        var body = await ctx.Request.ReadFromJsonAsync<JsonElement>();
        if (body.TryGetProperty("force", out var f) && f.ValueKind == JsonValueKind.True)
            force = true;
    }
    catch { }

    var today = DateTime.UtcNow.Date;
    if (!force)
    {
        var existing = GetExistingReportIdForToday(today);
        if (existing != null)
        {
            return Results.Ok(new { id = existing, file = $"/reports/{existing}/file" });
        }
    }

    var reportId = Guid.NewGuid().ToString("N");
    var reportData = GetReportData();
    var html = RenderHtml(reportData, today);
    var pdfPath = Path.Combine(reportsDir, $"{reportId}.pdf");
    var testPdfPath = Path.Combine(reportsDir, "test.pdf");
    await RenderPdf(html, pdfPath);
    await RenderPdf(html, testPdfPath);

    var createdAt = DateTime.UtcNow;
    InsertReport(reportId, pdfPath.Replace("\\", "/"), createdAt);

    return Results.Created($"/reports/{reportId}", new { id = reportId, file = $"/reports/{reportId}/file" });
});

app.MapGet("/reports/{id}", (string id) =>
{
    var record = GetReportRecord(id);
    if (record == null) return Results.NotFound();
    return Results.Json(record);
});

app.MapGet("/reports/{id}/file", (string id) =>
{
    var record = GetReportRecord(id);
    if (record == null) return Results.NotFound();
    var fullPath = record.Path;
    if (!Path.IsPathRooted(fullPath))
        fullPath = Path.Combine(Directory.GetCurrentDirectory(), fullPath);
    if (!File.Exists(fullPath)) return Results.NotFound();
    var fileName = Path.GetFileName(fullPath);
    return Results.File(fullPath, "application/pdf", fileName);
});

app.Run("http://localhost:5288");

void InitializeDatabase()
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS orders (
            id INTEGER PRIMARY KEY,
            customer TEXT NOT NULL,
            product TEXT NOT NULL,
            amount REAL NOT NULL,
            created_at TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS reports (
            id TEXT PRIMARY KEY,
            path TEXT NOT NULL,
            created_at TEXT NOT NULL
        );
    ";
    cmd.ExecuteNonQuery();
}

int GetOrdersCount()
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM orders";
    return Convert.ToInt32(cmd.ExecuteScalar());
}

void SeedDatabase()
{
    var products = new[] { "Laptop", "Phone", "Tablet", "Headphones", "Mouse", "Keyboard" };
    var rnd = new Random();
    var now = DateTime.UtcNow;
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    using var tx = conn.BeginTransaction();
    var insertCmd = conn.CreateCommand();
    insertCmd.Transaction = tx;
    insertCmd.CommandText = "INSERT OR IGNORE INTO orders (id, customer, product, amount, created_at) VALUES (@id, @customer, @product, @amount, @created_at)";
    insertCmd.Parameters.Add(new SqliteParameter("@id", SqliteType.Integer));
    insertCmd.Parameters.Add(new SqliteParameter("@customer", SqliteType.Text));
    insertCmd.Parameters.Add(new SqliteParameter("@product", SqliteType.Text));
    insertCmd.Parameters.Add(new SqliteParameter("@amount", SqliteType.Real));
    insertCmd.Parameters.Add(new SqliteParameter("@created_at", SqliteType.Text));

    var currentCount = GetOrdersCount();
    var need = 200 - currentCount;
    if (need <= 0) return;
    for (int i = 0; i < need; i++)
    {
        var id = rnd.Next(1000000, 9999999);
        var customer = $"Customer{id % 100}";
        var product = products[rnd.Next(products.Length)];
        var amount = Math.Round(rnd.NextDouble() * (200 - 5) + 5, 2);
        var daysAgo = rnd.Next(0, 30);
        var createdAt = now.AddDays(-daysAgo).ToString("yyyy-MM-ddTHH:mm:ssZ");
        insertCmd.Parameters["@id"].Value = id;
        insertCmd.Parameters["@customer"].Value = customer;
        insertCmd.Parameters["@product"].Value = product;
        insertCmd.Parameters["@amount"].Value = amount;
        insertCmd.Parameters["@created_at"].Value = createdAt;
        insertCmd.ExecuteNonQuery();
    }
    tx.Commit();
}




ReportData GetReportData()
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var data = new ReportData();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*), COALESCE(SUM(amount), 0) FROM orders";
    using var r = cmd.ExecuteReader();
    if (r.Read())
    {
        data.TotalOrders = Convert.ToInt32(r[0]);
        data.TotalRevenue = Math.Round(Convert.ToDouble(r[1]), 2);
    }
    r.Close();

    cmd.CommandText = @"
        SELECT product, SUM(amount) as revenue
        FROM orders
        GROUP BY product
        ORDER BY revenue DESC
        LIMIT 5
    ";
    using var r2 = cmd.ExecuteReader();
    while (r2.Read())
    {
        data.TopProducts.Add(new TopProduct { Product = r2.GetString(0), Revenue = Math.Round(r2.GetDouble(1), 2) });
    }
    r2.Close();

    var end = DateTime.UtcNow.Date;
    var start = end.AddDays(-6);
    cmd.CommandText = @"
        SELECT substr(created_at, 1, 10) as d, COUNT(*) as c
        FROM orders
        WHERE date(created_at) >= date(@start) AND date(created_at) <= date(@end)
        GROUP BY d
        ORDER BY d
    ";
    cmd.Parameters.Clear();
    cmd.Parameters.Add(new SqliteParameter("@start", start.ToString("yyyy-MM-dd")));
    cmd.Parameters.Add(new SqliteParameter("@end", end.ToString("yyyy-MM-dd")));
    using var r3 = cmd.ExecuteReader();
    while (r3.Read())
    {
        data.OrdersPerDayLast7.Add(new DailyOrders { Date = r3.GetString(0), Count = Convert.ToInt32(r3[1]) });
    }
    return data;
}

string? GetExistingReportIdForToday(DateTime today)
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id FROM reports WHERE substr(created_at, 1, 10) = @d ORDER BY created_at DESC LIMIT 1";
    cmd.Parameters.Add(new SqliteParameter("@d", today.ToString("yyyy-MM-dd")));
    var res = cmd.ExecuteScalar();
    return res?.ToString();
}

void InsertReport(string id, string path, DateTime createdAt)
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "INSERT OR REPLACE INTO reports (id, path, created_at) VALUES (@id, @path, @created_at)";
    cmd.Parameters.Add(new SqliteParameter("@id", id));
    cmd.Parameters.Add(new SqliteParameter("@path", path));
    cmd.Parameters.Add(new SqliteParameter("@created_at", createdAt.ToString("yyyy-MM-ddTHH:mm:ssZ")));
    cmd.ExecuteNonQuery();
}

ReportRecord? GetReportRecord(string id)
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, path, created_at FROM reports WHERE id = @id";
    cmd.Parameters.Add(new SqliteParameter("@id", id));
    using var r = cmd.ExecuteReader();
    if (r.Read())
    {
        return new ReportRecord { Id = r.GetString(0), Path = r.GetString(1), CreatedAt = r.GetString(2) };
    }
    return null;
}

string RenderHtml(ReportData data, DateTime generatedOn)
{
    var sb = new StringBuilder();
    sb.AppendLine("<!DOCTYPE html>");
    sb.AppendLine("<html><head><meta charset=\"utf-8\"><title>Report</title>");
    sb.AppendLine("<style>");
    sb.AppendLine("@page { size: A4; margin: 20mm; }");
    sb.AppendLine("body { font-family: Arial, sans-serif; font-size: 12px; }");
    sb.AppendLine("h1 { font-size: 18px; margin-bottom: 10px; }");
    sb.AppendLine("h2 { font-size: 14px; margin-top: 16px; }");
    sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 10px; }");
    sb.AppendLine("thead { display: table-header-group; }");
    sb.AppendLine("th, td { border: 1px solid #ccc; padding: 6px; text-align: left; }");
    sb.AppendLine("tr { page-break-inside: avoid; }");
    sb.AppendLine(".kpi { display: flex; gap: 20px; margin-top: 10px; }");
    sb.AppendLine(".kpi div { border: 1px solid #eee; padding: 8px; }");
    sb.AppendLine("</style></head><body>");
    sb.AppendLine($"<h1>Orders Report</h1>");
    sb.AppendLine($"<p>Generated: {generatedOn:yyyy-MM-dd}</p>");
    sb.AppendLine("<div class=\"kpi\">");
    sb.AppendLine($"<div><strong>Total Orders</strong>: {data.TotalOrders}</div>");
    sb.AppendLine($"<div><strong>Total Revenue</strong>: {data.TotalRevenue:F2}</div>");
    sb.AppendLine("</div>");
    sb.AppendLine("<h2>Top 5 Products by Revenue</h2>");
    sb.AppendLine("<table><thead><tr><th>Product</th><th>Revenue</th></tr></thead><tbody>");
    foreach (var p in data.TopProducts)
    {
        sb.AppendLine($"<tr><td>{Escape(p.Product)}</td><td>{p.Revenue:F2}</td></tr>");
    }
    sb.AppendLine("</tbody></table>");
    sb.AppendLine("<h2>Orders Per Day (Last 7 Days)</h2>");
    sb.AppendLine("<table><thead><tr><th>Date</th><th>Orders</th></tr></thead><tbody>");
    foreach (var d in data.OrdersPerDayLast7)
    {
        sb.AppendLine($"<tr><td>{Escape(d.Date)}</td><td>{d.Count}</td></tr>");
    }
    sb.AppendLine("</tbody></table>");
    sb.AppendLine("</body></html>");
    return sb.ToString();
}

string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

async Task RenderPdf(string html, string outputPath)
{
    try
    {
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = "msedge",
            Headless = true,
            ExecutablePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
        });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(html);
        await page.PdfAsync(new PagePdfOptions
        {
            Path = outputPath,
            Format = "A4",
            PrintBackground = true
        });
        await browser.CloseAsync();
    }
    catch
    {
        await RenderPdfViaEdgeHeadless(html, outputPath);
    }
}

async Task RenderPdfViaEdgeHeadless(string html, string outputPath)
{
    var tempHtml = Path.Combine(Path.GetTempPath(), $"report_{Guid.NewGuid():N}.html");
    await File.WriteAllTextAsync(tempHtml, html, Encoding.UTF8);
    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        Arguments = $"--headless=new --disable-gpu --no-pdf-header-footer --print-to-pdf=\"{outputPath}\" \"file:///{tempHtml.Replace('\\', '/')}\"",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    using var p = System.Diagnostics.Process.Start(psi);
    if (p == null) throw new Exception("Failed to start Edge");
    await p.WaitForExitAsync();
    try { File.Delete(tempHtml); } catch { }
}

class ReportRecord
{
    public string Id { get; set; } = "";
    public string Path { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}

class DailyOrders
{
    public string Date { get; set; } = "";
    public int Count { get; set; }
}

class TopProduct
{
    public string Product { get; set; } = "";
    public double Revenue { get; set; }
}

class ReportData
{
    public int TotalOrders { get; set; }
    public double TotalRevenue { get; set; }
    public List<TopProduct> TopProducts { get; set; } = new();
    public List<DailyOrders> OrdersPerDayLast7 { get; set; } = new();
}

