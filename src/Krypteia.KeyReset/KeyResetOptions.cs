namespace Krypteia.KeyReset;

/// <summary>
/// Configuration for the Krypteia key reset flow.
/// </summary>
public sealed class KeyResetOptions
{
    /// <summary>
    /// How long a reset token remains valid after issuance. Default: 15 minutes.
    /// </summary>
    /// <remarks>
    /// Short TTLs limit the window in which a leaked token could be used.
    /// Don't go shorter than 5 minutes — users need time to receive the email,
    /// open it, and click the link.
    /// </remarks>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Maximum number of reset attempts (used or unused) allowed per user
    /// within <see cref="RateLimitWindow"/>. Default: 3.
    /// </summary>
    public int MaxAttemptsPerUser { get; set; } = 3;

    /// <summary>
    /// The window over which <see cref="MaxAttemptsPerUser"/> is measured.
    /// Default: 1 hour.
    /// </summary>
    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// The base URL of the page that handles the reset link in the user's
    /// browser. The token is appended as a <c>?token=...</c> query parameter.
    /// </summary>
    /// <remarks>
    /// Example: <c>"https://app.example.com/reset"</c>. The resulting email
    /// link will be <c>https://app.example.com/reset?token=AbCdEf123...</c>.
    /// </remarks>
    public string ResetUrlBase { get; set; } = string.Empty;

    /// <summary>
    /// The "From" address used on outgoing reset emails. Implementations of
    /// <see cref="Abstractions.IEmailSender"/> may override this if they
    /// enforce a fixed sender address.
    /// </summary>
    public string FromAddress { get; set; } = "no-reply@example.com";

    /// <summary>
    /// The subject line of the reset email. Localize if needed.
    /// </summary>
    public string EmailSubject { get; set; } = "Reset your encryption key";
}