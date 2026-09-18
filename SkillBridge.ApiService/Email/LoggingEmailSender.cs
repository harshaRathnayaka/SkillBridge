using Microsoft.Extensions.Logging;

namespace SkillBridge.ApiService.Email;

// Dev-only stand-in: logs instead of actually sending. Replace with a real provider
// (e.g. SendGrid, Amazon SES) before any real deployment.
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Email to {ToEmail}: {Subject}\n{Body}", toEmail, subject, body);
        return Task.CompletedTask;
    }
}
