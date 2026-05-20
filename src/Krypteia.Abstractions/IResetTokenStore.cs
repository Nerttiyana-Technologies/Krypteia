namespace Krypteia.Abstractions;

/// <summary>
/// Stores and retrieves one-time reset tokens.
/// </summary>
/// <remarks>
/// <para>
/// <b>Implementations must never store tokens in plaintext.</b> The reset
/// service hashes the token with SHA-256 before calling
/// <see cref="StoreAsync"/>; the store sees the hash, not the original.
/// This means an attacker who reads the database cannot use the stored
/// values to complete resets.
/// </para>
/// <para>
/// The default implementation persists tokens to the database via EF Core.
/// Consumers running Redis or similar can implement this interface against
/// their cache instead — tokens are short-lived (default 15 minutes) so an
/// ephemeral cache is acceptable, though crashes mid-flow will force users
/// to request a new reset.
/// </para>
/// </remarks>
public interface IResetTokenStore
{ 
    /// <summary>
    /// 
    /// </summary>
    /// <param name="record"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task StoreAsync(
        ResetTokenRecord record,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a token record by its hash. Returns <c>null</c> if no record
    /// exists with that hash.
    /// </summary>
    /// <remarks>
    /// The returned record may be expired or already used; the reset service
    /// checks these conditions and audits each outcome separately.
    /// </remarks>
    Task<ResetTokenRecord?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a token as used. Idempotent — calling this on an already-used
    /// token must not throw.
    /// </summary>
    Task MarkUsedAsync(
        string tokenHash,
        DateTimeOffset usedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts how many reset attempts (used or unused) exist for a given user
    /// within the supplied time window. Used by the rate-limiting check in
    /// the reset service.
    /// </summary>
    Task<int> CountRecentAttemptsAsync(
        string userId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A single reset token record as persisted by an <see cref="IResetTokenStore"/>.
/// </summary>
/// <param name="TokenHash">SHA-256 hash of the token sent to the user. The token value itself is never persisted.</param>
/// <param name="UserId">The user the token was issued for.</param>
/// <param name="CreatedAt">When the token was issued. UTC.</param>
/// <param name="ExpiresAt">When the token stops being valid. UTC.</param>
/// <param name="UsedAt">When the token was consumed, or <c>null</c> if still unused.</param>
public sealed record ResetTokenRecord(
    string TokenHash,
    string UserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? UsedAt);