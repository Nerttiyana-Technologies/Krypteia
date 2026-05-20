namespace Krypteia.Abstractions;

/// <summary>
/// Records audit events for cryptographic operations.
/// </summary>
/// <remarks>
/// <para>
/// Audit records are required for CMMC Level 2, HIPAA, PCI-DSS, and SOC 2 compliance.
/// Implementations should write to append-only storage with a retention period of
/// at least 7 years (configurable per deployment).
/// </para>
/// <para>
/// <b>Critical:</b> implementations must never log plaintext data, private keys,
/// passwords, or any value derived from them. Field names and operation types
/// are loggable; field values are not.
/// </para>
/// </remarks>
public interface IAuditService
{
    /// <summary>
    /// Records an audit event for a cryptographic or key management operation.
    /// </summary>
    /// <param name="entry">The audit entry to record.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
