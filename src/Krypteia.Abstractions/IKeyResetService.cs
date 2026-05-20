namespace Krypteia.Abstractions;

/// <summary>
/// Manages the private-key reset flow for users who have lost access to their private key.
/// </summary>
/// <remarks>
/// <para>
/// The reset flow has two stages:
/// </para>
/// <list type="number">
///   <item>The user requests a reset; the service generates a one-time token and emails them a link.</item>
///   <item>The user clicks the link; the service validates the token, generates a new key pair,
///         re-encrypts the user's existing data with the new public key, and returns the new private key.</item>
/// </list>
/// <para>
/// Tokens are stored as SHA-256 hashes (never as plaintext), expire after a configurable
/// TTL (default 15 minutes), and can be used at most once. All attempts — successful
/// or otherwise — are recorded via <see cref="IAuditService"/>.
/// </para>
/// </remarks>
public interface IKeyResetService
{
    /// <summary>
    /// Initiates a key reset for a user. Generates a one-time token, persists its hash,
    /// and sends a reset link to the user's registered email address.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="ipAddress">The source IP for audit and rate-limiting purposes.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see cref="KeyResetInitiationResult.Success"/> if the link was sent.
    /// <see cref="KeyResetInitiationResult.RateLimited"/> if the user has exceeded
    /// the configured number of reset attempts in the rate-limit window.
    /// </returns>
    /// <remarks>
    /// To avoid user enumeration, this method should return success even when the
    /// supplied user identifier does not exist — but should not actually send any email
    /// in that case. Implementations decide the exact policy.
    /// </remarks>
    Task<KeyResetInitiationResult> InitiateResetAsync(
        string userId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a key reset using a token previously delivered to the user.
    /// </summary>
    /// <param name="token">The reset token from the email link.</param>
    /// <param name="ipAddress">The source IP for audit purposes.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The new key pair on success. The private key must be delivered to the user
    /// over an HTTPS response, displayed once, and not stored on the server in plaintext.
    /// </returns>
    /// <exception cref="KrypteiaException">
    /// Thrown when the token is invalid, expired, already used, or any other reset
    /// failure occurs. The exception message must be generic to prevent attackers
    /// from learning why a particular token failed.
    /// </exception>
    Task<KeyPair> CompleteResetAsync(
        string token,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>The outcome of a reset initiation request.</summary>
public enum KeyResetInitiationResult
{
    /// <summary>Reset link was processed (link sent if user exists).</summary>
    Success,

    /// <summary>The user has exceeded the rate limit for reset attempts.</summary>
    RateLimited,
}
