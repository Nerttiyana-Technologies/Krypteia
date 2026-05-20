using Krypteia.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Krypteia.KeyReset;

/// <summary>
/// Production <see cref="IEmailSender"/> that sends mail via an SMTP server
/// using MailKit.
/// </summary>
/// <remarks>
/// <para>
/// MailKit is used in preference to the legacy <c>System.Net.Mail.SmtpClient</c>,
/// which Microsoft itself recommends against for new development. MailKit
/// handles modern TLS negotiation correctly.
/// </para>
/// <para>
/// A fresh connection is opened per message. For the key reset flow — which
/// sends mail infrequently, only when a user requests a reset — this is
/// simpler and safer than pooling, and the cost is negligible.
/// </para>
/// </remarks>
public sealed partial class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly string _fromAddress;
    private readonly ILogger<SmtpEmailSender> _logger;

    /// <summary>Initializes a new instance.</summary>
    public SmtpEmailSender(
        IOptions<EmailSenderOptions> emailOptions,
        IOptions<KeyResetOptions> resetOptions,
        ILogger<SmtpEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(emailOptions);
        ArgumentNullException.ThrowIfNull(resetOptions);

        _options = emailOptions.Value.Smtp;
        _fromAddress = resetOptions.Value.FromAddress;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(_fromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;

        var bodyBuilder = new BodyBuilder { TextBody = message.BodyText };
        if (!string.IsNullOrEmpty(message.BodyHtml))
        {
            bodyBuilder.HtmlBody = message.BodyHtml;
        }

        mime.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            SecureSocketOptions socketOptions = _options.UseTls
                ? SecureSocketOptions.Auto
                : SecureSocketOptions.None;

            await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrEmpty(_options.Username))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken)
                    .ConfigureAwait(false);
            }

            await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);

            Log.EmailSent(_logger, message.To);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.EmailSendFailed(_logger, message.To, ex);
            throw new KrypteiaException("Failed to send email.", ex);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 2100,
            Level = LogLevel.Information,
            Message = "SMTP email sent to {Recipient}.")]
        public static partial void EmailSent(ILogger logger, string recipient);

        [LoggerMessage(
            EventId = 2101,
            Level = LogLevel.Error,
            Message = "SMTP email send to {Recipient} failed.")]
        public static partial void EmailSendFailed(ILogger logger, string recipient, Exception exception);
    }
}