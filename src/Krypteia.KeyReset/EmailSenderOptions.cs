namespace Krypteia.KeyReset;

/// <summary>
/// Selects and configures the email transport used by the key reset flow.
/// </summary>
/// <remarks>
/// Bind this from configuration, e.g. an <c>"EmailSender"</c> section in
/// <c>appsettings.json</c>. The <see cref="Provider"/> value decides which
/// <see cref="Krypteia.Abstractions.IEmailSender"/> implementation is registered.
/// </remarks>
public sealed class EmailSenderOptions
{
    /// <summary>
    /// Which email transport to use. Defaults to <see cref="EmailProvider.Console"/>
    /// so a misconfigured deployment fails safe (logs instead of silently
    /// not sending) rather than throwing at startup.
    /// </summary>
    public EmailProvider Provider { get; set; } = EmailProvider.Console;

    /// <summary>SMTP transport settings. Used only when <see cref="Provider"/> is <see cref="EmailProvider.Smtp"/>.</summary>
    public SmtpOptions Smtp { get; set; } = new();

    /// <summary>SendGrid transport settings. Used only when <see cref="Provider"/> is <see cref="EmailProvider.SendGrid"/>.</summary>
    public SendGridOptions SendGrid { get; set; } = new();
}

/// <summary>The available email transports.</summary>
public enum EmailProvider
{
    /// <summary>Logs emails instead of sending them. Development only.</summary>
    Console,

    /// <summary>Sends via an SMTP server using MailKit.</summary>
    Smtp,

    /// <summary>Sends via the SendGrid HTTP API.</summary>
    SendGrid,
}

/// <summary>Settings for the SMTP email transport.</summary>
public sealed class SmtpOptions
{
    /// <summary>SMTP server hostname, e.g. <c>"smtp.example.com"</c> or <c>"localhost"</c>.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>SMTP server port. Common values: 25 (plain), 587 (STARTTLS), 465 (implicit TLS).</summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// Whether to use TLS. When <c>true</c>, MailKit negotiates the best
    /// available option (STARTTLS on 587, implicit TLS on 465). When
    /// <c>false</c>, the connection is unencrypted — only acceptable for
    /// a trusted local relay (e.g. a development mail catcher).
    /// </summary>
    public bool UseTls { get; set; } = true;

    /// <summary>SMTP username, or empty for an unauthenticated relay.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// SMTP password. <b>Do not commit this to source control.</b> Supply it
    /// via user secrets, environment variables, or a secret manager.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>Settings for the SendGrid email transport.</summary>
public sealed class SendGridOptions
{
    /// <summary>
    /// SendGrid API key. <b>Do not commit this to source control.</b> Supply
    /// it via user secrets, environment variables, or a secret manager.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}