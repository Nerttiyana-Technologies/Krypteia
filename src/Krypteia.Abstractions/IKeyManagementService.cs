namespace Krypteia.Abstractions;

/// <summary>
/// Generates, retrieves, and manages user RSA key pairs.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are responsible for storing public keys and (optionally) encrypted backups
/// of private keys. Private keys must never be stored in plaintext on the server.
/// </para>
/// <para>
/// <b>Threat model assumption:</b> the persistence layer used by an implementation
/// is considered untrusted. A database administrator with full read access to the
/// underlying tables must not be able to recover any user's private key.
/// </para>
/// </remarks>
public interface IKeyManagementService
{
    /// <summary>
    /// Generates a new RSA key pair for a user.
    /// </summary>
    /// <param name="userId">The user identifier the key pair belongs to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The generated key pair. The private key portion must be delivered to the user only once and never persisted in plaintext on the server.</returns>
    Task<KeyPair> GenerateKeyPairAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current public key for a user. Public keys are not secret;
    /// any authorized caller may obtain them in order to encrypt data for that user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The user's current public key in PEM format, or <c>null</c> if no key exists.</returns>
    Task<string?> GetPublicKeyAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a user's public key. The corresponding private key is the user's
    /// responsibility to retain securely (typically on their device).
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="publicKey">The public key in PEM format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task StorePublicKeyAsync(
        string userId,
        string publicKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current key version for a user. Version is incremented on every
    /// reset; ciphertext is tagged with the version that produced it so multiple
    /// generations can coexist briefly during a reset.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The current key version, or 0 if no key exists.</returns>
    Task<int> GetKeyVersionAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the user's encrypted private key backup along with the master
    /// key identifier needed to unwrap it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This method is the controlled escape hatch from zero-knowledge.</b>
    /// Callers with access to the master key (and only those callers) can use
    /// the returned blob to recover the user's old private key — typically as
    /// part of a reset flow that re-encrypts the user's data under a new key
    /// before discarding the old one.
    /// </para>
    /// <para>
    /// Implementations must audit every call to this method. The reset flow
    /// does so via <see cref="IAuditService"/> with operation
    /// <see cref="AuditOperation.PrivateKeyBackupAccess"/>.
    /// </para>
    /// </remarks>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The encrypted backup, or <c>null</c> if no key exists for the user.</returns>
    Task<EncryptedKeyBackup?> GetEncryptedBackupAsync(
        string userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A user's encrypted private key backup with the metadata needed to unwrap it.
/// </summary>
/// <param name="WrappedPrivateKey">The Base64-encoded AES-GCM ciphertext of the private key.</param>
/// <param name="MasterKeyId">The identifier of the master key that wrapped this backup. Pass to <see cref="IMasterKeyProvider.GetMasterKeyAsync"/> to retrieve the key material needed for unwrap.</param>
/// <param name="KeyVersion">The version number of the wrapped key pair.</param>
public sealed record EncryptedKeyBackup(
    string WrappedPrivateKey,
    string MasterKeyId,
    int KeyVersion);