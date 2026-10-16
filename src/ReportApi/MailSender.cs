using System.Net.Mail;

public sealed class MailSender
{
    readonly IConfiguration _config;
    readonly ILogger<MailSender> _log;

    public MailSender(IConfiguration config, ILogger<MailSender> log)
    {
        _config = config;
        _log = log;
    }

    /// <summary>Emails the report link (never the attachment). Failures are logged and swallowed.</summary>
    public async Task SendReportLinkAsync(string id)
    {
        try
        {
            if (!_config.GetValue("Smtp:Enabled", true))
            {
                _log.LogInformation("SMTP disabled; skipping email for report {Id}.", id);
                return;
            }

            var host = _config["Smtp:Host"] ?? "127.0.0.1";
            var port = _config.GetValue("Smtp:Port", 1025);
            var from = _config["Smtp:From"] ?? "reports@flyrank.local";
            var to = _config["Smtp:To"] ?? "owner@flyrank.local";
            var baseUrl = (_config["Smtp:BaseUrl"] ?? "http://localhost:5188").TrimEnd('/');
            var link = $"{baseUrl}/reports/{id}/file";

            using var client = new SmtpClient(host, port);
            using var message = new MailMessage(from, to)
            {
                Subject = $"FlyRank report {id}",
                Body = $"Your FlyRank report is ready.\n\nDownload it here: {link}\n"
            };
            await client.SendMailAsync(message);
            _log.LogInformation("Emailed report link for {Id} to {To}.", id, to);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Email for report {Id} failed; report generation continues.", id);
        }
    }
}
