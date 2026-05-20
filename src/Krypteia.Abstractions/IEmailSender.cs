namespace Krypteia.Abstractions;

/// <summary>
/// Sends emails. Used by the key reset flow to deliver one-time reset links
/// to the user's registered email address.
/// </summary>
/// <remarks>
/// <para>
/// Krypteia does not ship a production-grade email sender. Implementations
/// should wrap a transactional email provider (SendGrid, Amazon SES, Postmark,
/// Mailgun, etc.) or an SMTP server you operate yourself.
/// </para>
/// <para>
/// <b>Reliability matters.</b> If <see cref="SendAsync"/> returns successfully,
/// the key reset service treats the email as delivered and proceeds. A failure
/// must surface as an exception so the caller can retry or surface an error.
/// </para>
/// </remarks>
public interface IEmailSender
{
    /// <summary>
    /// Sends an email. The implementation is responsible for transport,
    /// authentication, and any retry policy.
    /// </summary>
    /// <param name="message">The message to send. Already populated with sender, recipient, subject, and body.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// A simple, transport-agnostic email message. Suitable for the small set of
/// messages Krypteia itself sends (just reset links today).
/// </summary>
/// <param name="To">The recipient's email address.</param>
/// <param name="Subject">The subject line.</param>
/// <param name="BodyHtml">HTML body. May be <c>null</c> if only a plaintext body is provided.</param>
/// <param name="BodyText">Plaintext body. Recommended for accessibility and for clients that block HTML.</param>
public sealed record EmailMessage(
    string To,
    string Subject,
    string? BodyHtml,
    string BodyText);