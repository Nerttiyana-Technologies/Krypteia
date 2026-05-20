using Krypteia.Abstractions;
using Krypteia.KeyReset;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Krypteia.EmailSender.SendGrid;

/// <summary>
/// Production <see cref="IEmailSender"/> that sends mail via the SendGrid HTTP API.
/// </summary>
/// <remarks>
/// <para>
/// SendGrid is an HTTP-based transactional email service. Unlike SMTP, there
/// is no persistent connection — each send is a single HTTPS request — so no
/// connection management is needed here.
/// </para>
/// <para>
/// The API key is read from <see cref="SendGridOptions.ApiKey"/>. Supply it
/// through user secrets, environment variables, or a secret manager — never
/// commit it to source control.
/// </para>
/// </remarks>
public sealed partial class SendGridEmailSender : IEmailSender
{
    private readonly string _apiKey;
    private readonly string _fromAddress;
    private readonly ILogger<SendGridEmailSender> _logger;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="emailOptions">The email sender options; the <see cref="EmailSenderOptions.SendGrid"/> section is used.</param>
    /// <param name="resetOptions">The key reset options; <see cref="KeyResetOptions.FromAddress"/> is used as the sender address.</param>
    /// <param name="logger">A logger.</param>
    public SendGridEmailSender(
        IOptions<EmailSenderOptions> emailOptions,
        IOptions<KeyResetOptions> resetOptions,
        ILogger<SendGridEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(emailOptions);
        ArgumentNullException.ThrowIfNull(resetOptions);

        _apiKey = emailOptions.Value.SendGrid.ApiKey;
        _fromAddress = resetOptions.Value.FromAddress;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException(
                "SendGrid API key is not configured. Set EmailSender:SendGrid:ApiKey.");
        }
    }

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var client = new SendGridClient(_apiKey);

            var from = new EmailAddress(_fromAddress);
            var to = new EmailAddress(message.To);

            SendGridMessage mail = MailHelper.CreateSingleEmail(
                from,
                to,
                message.Subject,
                plainTextContent: message.BodyText,
                htmlContent: string.IsNullOrEmpty(message.BodyHtml) ? null : message.BodyHtml);

            Response response = await client.SendEmailAsync(mail, cancellationToken)
                .ConfigureAwait(false);

            // SendGrid returns 2xx on success (typically 202 Accepted).
            if ((int)response.StatusCode is < 200 or >= 300)
            {
                string body = await response.Body.ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);

                Log.EmailRejected(_logger, message.To, (int)response.StatusCode);

                throw new KrypteiaException(
                    $"SendGrid rejected the message with status {(int)response.StatusCode}.");
            }

            Log.EmailSent(_logger, message.To);
        }
        catch (KrypteiaException)
        {
            // Already wrapped — let it through unchanged.
            throw;
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
            EventId = 2200,
            Level = LogLevel.Information,
            Message = "SendGrid email sent to {Recipient}.")]
        public static partial void EmailSent(ILogger logger, string recipient);

        [LoggerMessage(
            EventId = 2201,
            Level = LogLevel.Warning,
            Message = "SendGrid rejected email to {Recipient} with status {StatusCode}.")]
        public static partial void EmailRejected(ILogger logger, string recipient, int statusCode);

        [LoggerMessage(
            EventId = 2202,
            Level = LogLevel.Error,
            Message = "SendGrid email send to {Recipient} failed.")]
        public static partial void EmailSendFailed(ILogger logger, string recipient, Exception exception);
    }
}