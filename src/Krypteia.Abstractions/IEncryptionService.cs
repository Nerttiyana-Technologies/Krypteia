namespace Krypteia.Abstractions;

/// <summary>
/// Performs asymmetric encryption and decryption operations on sensitive data
/// using a caller-supplied public/private key pair.
/// </summary>
/// <remarks>
/// <para>
/// Implementations of <see cref="IEncryptionService"/> must use only published, standard
/// cryptographic algorithms (e.g., RSA-2048 with OAEP-SHA256 padding). Custom or
/// proprietary algorithms are forbidden.
/// </para>
/// <para>
/// <b>Thread safety:</b> implementations must be safe for concurrent use across threads.
/// Stateful internal buffers must not leak between calls.
/// </para>
/// <para>
/// <b>Security:</b> implementations must zero out sensitive byte arrays after use
/// (typically via <c>CryptographicOperations.ZeroMemory</c>) and must never log
/// plaintext data, private keys, or any data that could be used to reconstruct them.
/// </para>
/// </remarks>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts the supplied plaintext using a public key.
    /// </summary>
    /// <param name="plaintext">The data to encrypt. Must not be null.</param>
    /// <param name="publicKey">The recipient's public key in PEM format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The encrypted ciphertext as a Base64-encoded string suitable for storage
    /// in text-based fields (e.g., database columns of type <c>NVARCHAR</c>).
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="plaintext"/> or <paramref name="publicKey"/> is null.
    /// </exception>
    /// <exception cref="KrypteiaException">
    /// Thrown when encryption fails for any cryptographic reason. Inner exception
    /// details are preserved but should not be exposed to end users.
    /// </exception>
    Task<string> EncryptAsync(
        string plaintext,
        string publicKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts ciphertext produced by <see cref="EncryptAsync"/> using the recipient's private key.
    /// </summary>
    /// <param name="ciphertext">The Base64-encoded ciphertext to decrypt.</param>
    /// <param name="privateKey">The recipient's private key in PEM format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The original plaintext.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="ciphertext"/> or <paramref name="privateKey"/> is null.
    /// </exception>
    /// <exception cref="KrypteiaException">
    /// Thrown when decryption fails for any cryptographic reason. To prevent
    /// information disclosure, the exception message must not distinguish between
    /// "wrong key" and "corrupt ciphertext" — both result in the same generic error.
    /// </exception>
    Task<string> DecryptAsync(
        string ciphertext,
        string privateKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts binary data. Suitable for files or other non-text payloads.
    /// </summary>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <param name="publicKey">The recipient's public key in PEM format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The encrypted bytes.</returns>
    Task<byte[]> EncryptBytesAsync(
        ReadOnlyMemory<byte> plaintext,
        string publicKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts binary data produced by <see cref="EncryptBytesAsync"/>.
    /// </summary>
    /// <param name="ciphertext">The encrypted bytes.</param>
    /// <param name="privateKey">The recipient's private key in PEM format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The original plaintext bytes.</returns>
    Task<byte[]> DecryptBytesAsync(
        ReadOnlyMemory<byte> ciphertext,
        string privateKey,
        CancellationToken cancellationToken = default);
}
