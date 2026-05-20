namespace Krypteia.EntityFrameworkCore;

/// <summary>
/// EF Core entity representing a user's stored key material.
/// </summary>
/// <remarks>
/// <para>
/// This table contains:
/// </para>
/// <list type="bullet">
///   <item><description><b>Public key</b> — plaintext PEM. Safe to read; the threat model assumes anyone can have a user's public key.</description></item>
///   <item><description><b>Encrypted private key backup</b> — AES-256-GCM ciphertext wrapped by the master key. Useless without the master key.</description></item>
///   <item><description><b>Master key identifier</b> — records which master key version encrypted this row, so backups remain decryptable across master key rotations.</description></item>
/// </list>
/// <para>
/// All column types are plain (string, int, DateTimeOffset) to remain
/// compatible with SQL Server, PostgreSQL, MariaDB, and SQLite providers.
/// No JSONB, no provider-specific functions, no raw SQL.
/// </para>
/// </remarks>
public sealed class UserKeyEntity
{
    /// <summary>
    /// The user identifier this row pertains to. Primary key.
    /// </summary>
    /// <remarks>
    /// Length-capped at 64 characters; the application is responsible for
    /// ensuring identifiers fit. Standard formats (Guid, ULID, integer-as-string,
    /// auth-provider subject claim) all fit comfortably.
    /// </remarks>
    public required string UserId { get; set; }

    /// <summary>
    /// PEM-encoded public key (with -----BEGIN/END PUBLIC KEY----- markers).
    /// </summary>
    public required string PublicKey { get; set; }

    /// <summary>
    /// Base64-encoded AES-GCM ciphertext containing the encrypted private key
    /// <c>[version=0x01][12-byte nonce][16-byte tag][ciphertext]</c>.
    /// </summary>
    /// <remarks>
    /// Storing this column does not weaken the threat model: the contents are
    /// useless without the master key that wrapped them.
    /// </remarks>
    ///
    public required string EncryptedPrivateKeyBackup { get; set; }

    /// <summary>
    /// The version number of this key pair. Incremented on every reset so
    /// ciphertext tagged with an older version can still be identified and,
    /// during the transition window, re-encrypted under the new key.
    /// </summary>
    public int KeyVersion { get; set; } = 1;

    /// <summary>
    /// The identifier of the master key that was current when this row was
    /// written. Used at unwrap time to retrieve the correct master key after
    /// rotation.
    /// </summary>
    public required string MasterKeyId { get; set; }

    /// <summary>
    /// When this row was created. Always stored in UTC.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this row was last updated. Always stored in UTC.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}