namespace Krypteia.EntityFrameworkCore;

/// <summary>
/// EF Core entity representing a single reset token. One row per attempt;
/// historical rows are retained for rate-limiting and audit purposes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only the SHA-256 hash of the token is persisted.</b> The plaintext token
/// goes out in the email and is never written to the database. An attacker who
/// reads the row cannot recover the original token to complete a reset.
/// </para>
/// <para>
/// Rows are not deleted when used or expired — they're retained so the
/// rate-limiter can count recent attempts and so the audit log has a
/// corresponding storage record. Applications that want to prune ancient
/// rows can run a periodic job against this table; a row older than
/// is no longer needed.
/// </para>
/// </remarks>
public sealed class ResetTokenEntity
{
    /// <summary>
    /// SHA-256 hex digest of the token sent to the user. Primary key.
    /// </summary>
    /// <remarks>
    /// 64 characters of hex output. Fixed length — using a CHAR or NCHAR
    /// type would be slightly more compact but mixed-provider compatibility
    /// is cleaner with NVARCHAR/text.
    /// </remarks>
    public required string TokenHash { get; set; }

    /// <summary>The user this token was issued for.</summary>
    public required string UserId { get; set; }

    /// <summary>When the token was issued. UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the token stops being valid. UTC.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// When the token was consumed, or <c>null</c> if still unused.
    /// </summary>
    public DateTimeOffset? UsedAt { get; set; }
}