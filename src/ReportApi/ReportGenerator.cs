public sealed class ReportGenerator
{
    readonly MailSender _mail;
    readonly ILogger<ReportGenerator> _log;

    public ReportGenerator(MailSender mail, ILogger<ReportGenerator> log)
    {
        _mail = mail;
        _log = log;
    }

    public static string NewId() =>
        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];

    public static string PathFor(string id) => Path.Combine(Db.ReportsDir, $"{id}.pdf");

    /// <summary>Renders the PDF only; does not touch the reports table.</summary>
    public async Task<string> RenderAsync(string id, int days)
    {
        var data = ReportQueries.GetReportData(days);
        var orders = ReportQueries.GetTopOrders(60);
        var html = HtmlReport.Build(data, orders);
        var outPath = PathFor(id);
        await PdfRenderer.RenderToPdfAsync(html, outPath);
        return outPath;
    }

    /// <summary>Synchronous generation path: render, record as done, then email the link.</summary>
    public async Task<string> GenerateAsync(string id, int days)
    {
        days = ReportQueries.ClampDays(days);
        var outPath = await RenderAsync(id, days);
        Reports.Insert(id, outPath, DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss"), "done", days);
        _log.LogInformation("Report {Id} generated ({Days}-day window).", id, days);
        await NotifyAsync(id);
        return outPath;
    }

    /// <summary>Emails the report link. Never throws - email failures must not break generation.</summary>
    public Task NotifyAsync(string id) => _mail.SendReportLinkAsync(id);
}
