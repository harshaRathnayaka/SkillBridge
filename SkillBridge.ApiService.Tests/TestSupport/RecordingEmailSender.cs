using SkillBridge.ApiService.Email;

namespace SkillBridge.ApiService.Tests.TestSupport;

// Test double for IEmailSender — captures what would have been sent so tests can assert on
// it (e.g. extract a password-reset token) without a real mailbox.
public class RecordingEmailSender : IEmailSender
{
    public record SentEmail(string To, string Subject, string Body);

    private readonly List<SentEmail> _sent = [];
    public IReadOnlyList<SentEmail> SentEmails => _sent;

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        _sent.Add(new SentEmail(toEmail, subject, body));
        return Task.CompletedTask;
    }
}
