using Krypteia.Abstractions;
using Microsoft.Extensions.Logging;

namespace Krypteia.KeyReset;

/// <summary>
/// Development-only <see cref="IEmailSender"/> that logs emails instead of
/// sending them.
/// </summary>
/// <remarks>
/// <para>
/// Suitable for local development, integration tests, and any environment
/// where you want to see the reset link without hooking up an actual email
/// provider. <b>Never register this in production</b> — replace it with a
/// real implementation backed by SendGrid, Amazon SES, Postmark, or similar.
/// </para>
/// <para>
/// The full body (including the reset URL with the token) is written at
/// <see cref="LogLevel.Information"/>. This is intentional for dev use; do not
/// enable Information-level logging on the Krypteia namespace in production
/// or you'll leak tokens via the log pipeline.
/// </para>
/// </remarks>
public sealed partial class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    /// <summary>Initializes a new instance.</summary>
    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        Log.EmailSent(_logger, message.To, message.Subject, message.BodyText);
        return Task.CompletedTask;
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 2000,
            Level = LogLevel.Information,
            Message =
                "[ConsoleEmailSender] [DEV ONLY] Email would be sent.\n" +
                "  To: {To}\n" +
                "  Subject: {Subject}\n" +
                "  Body:\n{Body}")]
        public static partial void EmailSent(
            ILogger logger,
            string to,
            string subject,
            string body);
    }
}