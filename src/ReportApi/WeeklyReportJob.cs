public sealed class WeeklyReportJob : BackgroundService
{
    readonly ReportGenerator _generator;
    readonly ILogger<WeeklyReportJob> _log;

    public WeeklyReportJob(ReportGenerator generator, ILogger<WeeklyReportJob> log)
    {
        _generator = generator;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var next = NextMondayAt8(now);
            _log.LogInformation("Weekly report scheduled for {Next} (local).", next);

            try
            {
                await Task.Delay(next - now, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            var id = ReportGenerator.NewId();
            try
            {
                await _generator.GenerateAsync(id, ReportQueries.DefaultDays);
                _log.LogInformation("Weekly report {Id} generated.", id);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Weekly report generation failed.");
            }
        }
    }

    /// <summary>Next Monday at 08:00 local time (today if it is Monday before 08:00).</summary>
    static DateTime NextMondayAt8(DateTime now)
    {
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
        var candidate = now.Date.AddDays(daysUntilMonday).AddHours(8);
        return candidate <= now ? candidate.AddDays(7) : candidate;
    }
}
