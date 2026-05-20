

namespace Krypteia.Abstractions;

/// <summary>
/// Supplies the master key used to wrap (encrypt) and unwrap (decrypt) user
/// private key backups stored in the Krypteia database.
/// </summary>
/// <remarks>
/// <para>
/// The master key is the linchpin of the Krypteia threat model. With it, the
/// holder can decrypt every user's private key backup in the database. Without
/// it, the database contents are inert.
/// </para>
/// <para>
/// <b>Storage location matters more than the implementation.</b> The master key
/// must live outside the database it protects — typically in a dedicated key
/// management system (Azure Key Vault, AWS KMS, HashiCorp Vault, on-prem HSM,
/// or for development, a file with restricted permissions in a directory the
/// database has no access to).
/// </para>
/// <para>
/// <b>Versioning.</b> Master keys are rotated periodically. Each backup record
/// is tagged with the <see cref="CurrentKeyId"/> that was current when it was
/// encrypted, so old backups can still be decrypted after a rotation.
/// Implementations must keep historical key material accessible by ID until
/// every backup encrypted with it has been re-wrapped under the new key.
/// </para>
/// </remarks>
public interface IMasterKeyProvider
{
    /// <summary>
    /// Gets the identifier of the master key that should be used for new
    /// encryption operations. Format is implementation-defined (e.g., a
    /// version string, a Key Vault key version URI, or a filename).
    /// </summary>
    string CurrentKeyId { get; }

    /// <summary>
    /// Retrieves master key material by identifier.
    /// </summary>
    /// <param name="keyId">
    /// The key identifier. Pass <see cref="CurrentKeyId"/> for encryption;
    /// pass the value recorded on a backup record for decryption.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The raw key material, typically 32 bytes (256 bits) for AES-256-GCM.
    /// Callers must zero the returned array via
    /// <c>CryptographicOperations.ZeroMemory</c> when finished.
    /// </returns>
    /// <exception cref="KrypteiaException">
    /// Thrown if no key with the given identifier exists or is accessible.
    /// </exception>
    Task<byte[]> GetMasterKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default);
}