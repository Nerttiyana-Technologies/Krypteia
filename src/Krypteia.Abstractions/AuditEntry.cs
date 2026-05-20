namespace Krypteia.Abstractions;

/// <summary>
/// A single audit log entry recording a cryptographic or key management operation.
/// </summary>
/// <param name="Timestamp">When the operation occurred. Always in UTC.</param>
/// <param name="Operation">The type of operation performed.</param>
/// <param name="UserId">The user the operation pertains to.</param>
/// <param name="ActorId">The user or service that performed the operation. May differ from <paramref name="UserId"/> when an authorized third party acts on behalf of a user.</param>
/// <param name="FieldName">The logical name of the field being encrypted or decrypted, when applicable. <b>Never</b> the field value.</param>
/// <param name="KeyVersion">The key version used for the operation.</param>
/// <param name="Result">Whether the operation succeeded, failed, or was denied.</param>
/// <param name="IpAddress">The source IP address of the request, when available.</param>
/// <param name="CorrelationId">A correlation identifier linking this audit record to a parent request (e.g., a trace ID).</param>
/// <param name="ErrorCode">A short machine-readable error code when <paramref name="Result"/> is not <see cref="AuditResult.Success"/>. Never an exception message or stack trace.</param>
public sealed record AuditEntry(
    DateTimeOffset Timestamp,
    AuditOperation Operation,
    string UserId,
    string? ActorId,
    string? FieldName,
    int KeyVersion,
    AuditResult Result,
    string? IpAddress = null,
    string? CorrelationId = null,
    string? ErrorCode = null);

/// <summary>
/// The category of operation being audited.
/// </summary>
public enum AuditOperation
{
    /// <summary>Encryption of a field value using a public key.</summary>
    Encrypt,

    /// <summary>Decryption of a field value using a private key.</summary>
    Decrypt,

    /// <summary>Generation of a new RSA key pair.</summary>
    KeyGeneration,

    /// <summary>A key reset was initiated by the user (e.g., reset link requested).</summary>
    KeyResetInitiated,

    /// <summary>A key reset was completed (new key pair issued, data re-encrypted).</summary>
    KeyResetCompleted,

    /// <summary>A key reset attempt was rejected (expired token, reused token, rate limit).</summary>
    KeyResetRejected,

    /// <summary>The user requested their public key. Generally low-sensitivity but logged for completeness.</summary>
    PublicKeyAccess,

    /// <summary>A privileged operation accessed an encrypted private key backup. Highly sensitive.</summary>
    PrivateKeyBackupAccess,
}

/// <summary>The outcome of an audited operation.</summary>
public enum AuditResult
{
    /// <summary>The operation completed successfully.</summary>
    Success,

    /// <summary>The operation failed due to an error (cryptographic, I/O, configuration).</summary>
    Failure,

    /// <summary>The operation was denied due to authorization or policy.</summary>
    Denied,
}
