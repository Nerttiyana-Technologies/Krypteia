namespace Krypteia.Abstractions;

/// <summary>
/// Wraps and unwraps user private keys using a symmetric master key.
/// </summary>
/// <remarks>
/// <para>
/// The "envelope encryption" pattern: the user's RSA private key (the inner
/// secret) is protected by a symmetric master key (the outer secret) via
/// authenticated encryption. This lets us store private key backups in the
/// database without giving the database holder any way to read them.
/// </para>
/// <para>
/// Implementations must use authenticated encryption (AES-GCM or equivalent).
/// Tampering with the stored ciphertext must cause <see cref="UnwrapAsync"/>
/// to fail rather than return corrupt data.
/// </para>
/// </remarks>
public interface IKeyEnvelope
{
    /// <summary>
    /// Encrypts a private key for storage using the master key identified by
    /// <paramref name="masterKeyId"/>.
    /// </summary>
    /// <param name="privateKeyPem">The PEM-encoded RSA private key to wrap.</param>
    /// <param name="masterKeyId">The master key identifier to use for encryption.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A self-contained Base64 string suitable for storage in a single text
    /// column. The format includes everything needed to decrypt (nonce, tag,
    /// ciphertext) but not the master key itself.
    /// </returns>
    Task<string> WrapAsync(
        string privateKeyPem,
        string masterKeyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts a wrapped private key produced by <see cref="WrapAsync"/>.
    /// </summary>
    /// <param name="wrappedKey">The wrapped key blob from storage.</param>
    /// <param name="masterKeyId">The master key identifier recorded with the backup.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The original PEM-encoded RSA private key.</returns>
    /// <exception cref="KrypteiaException">
    /// Thrown if the wrapped blob is malformed, has been tampered with, or
    /// the master key is wrong. The message is intentionally generic.
    /// </exception>
    Task<string> UnwrapAsync(
        string wrappedKey,
        string masterKeyId,
        CancellationToken cancellationToken = default);
}