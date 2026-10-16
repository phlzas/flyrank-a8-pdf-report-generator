using System.Threading.Channels;

public sealed class ReportWorker : BackgroundService
{
    readonly ReportQueue _queue;
    readonly ReportGenerator _generator;
    readonly ILogger<ReportWorker> _log;

    public ReportWorker(ReportQueue queue, ReportGenerator generator, ILogger<ReportWorker> log)
    {
        _queue = queue;
        _generator = generator;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Re-enqueue anything left pending by a previous run so a restart does not strand it.
        foreach (var id in Reports.GetPending())
        {
            _log.LogInformation("Re-enqueuing pending report {Id}.", id);
            await _queue.EnqueueAsync(id);
        }

        await foreach (var id in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            var days = Reports.Get(id)?.Days ?? ReportQueries.DefaultDays;
            try
            {
                await _generator.RenderAsync(id, days);
                Reports.SetStatus(id, "done");
                await _generator.NotifyAsync(id);
                _log.LogInformation("Background report {Id} completed.", id);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Background report {Id} failed.", id);
                Reports.SetStatus(id, "failed");
            }
        }
    }
}
