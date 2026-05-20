using System.Security.Cryptography;
using System.Text;
using Krypteia.Abstractions;

namespace Krypteia;

/// <summary>
/// Default implementation of <see cref="IEncryptionService"/> using RSA-2048 with OAEP-SHA256 padding.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses .NET's built-in <see cref="RSA"/> primitives, which on most
/// platforms delegate to OS-provided cryptographic libraries (CNG on Windows, OpenSSL/CommonCrypto
/// on Linux/macOS).
/// </para>
/// <para>
/// <b>Algorithm:</b> RSA-2048 with OAEP padding using SHA-256 for both the hash and MGF1 functions.
/// This is the recommended configuration in NIST SP 800-56B Rev. 2.
/// </para>
/// <para>
/// <b>Payload size:</b> RSA-OAEP-SHA256 with a 2048-bit key can encrypt up to 190 bytes of plaintext
/// in a single operation. For larger payloads, use a hybrid scheme (RSA-wrapped AES key).
/// A hybrid extension will be added in a future release.
/// </para>
/// </remarks>
public sealed class RsaEncryptionService : IEncryptionService
{
    /// <inheritdoc />
    public Task<string> EncryptAsync(
        string plaintext,
        string publicKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(publicKey);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKey);

            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] ciphertextBytes = rsa.Encrypt(plaintextBytes, RSAEncryptionPadding.OaepSHA256);

            try
            {
                return Task.FromResult(Convert.ToBase64String(ciphertextBytes));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintextBytes);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Generic error message — never leak why encryption failed.
            throw new KrypteiaException("Encryption failed.", ex);
        }
    }

    /// <inheritdoc />
    public Task<string> DecryptAsync(
        string ciphertext,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(privateKey);

        cancellationToken.ThrowIfCancellationRequested();

        byte[]? plaintextBytes = null;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKey);

            byte[] ciphertextBytes = Convert.FromBase64String(ciphertext);
            plaintextBytes = rsa.Decrypt(ciphertextBytes, RSAEncryptionPadding.OaepSHA256);

            return Task.FromResult(Encoding.UTF8.GetString(plaintextBytes));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Generic error message — never distinguish between "wrong key" and "bad ciphertext".
            // This prevents padding-oracle-style information disclosure.
            throw new KrypteiaException("Decryption failed.", ex);
        }
        finally
        {
            if (plaintextBytes is not null)
            {
                CryptographicOperations.ZeroMemory(plaintextBytes);
            }
        }
    }

    /// <inheritdoc />
    public Task<byte[]> EncryptBytesAsync(
        ReadOnlyMemory<byte> plaintext,
        string publicKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKey);
            byte[] ciphertext = rsa.Encrypt(plaintext.Span, RSAEncryptionPadding.OaepSHA256);
            return Task.FromResult(ciphertext);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new KrypteiaException("Encryption failed.", ex);
        }
    }

    /// <inheritdoc />
    public Task<byte[]> DecryptBytesAsync(
        ReadOnlyMemory<byte> ciphertext,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKey);
            byte[] plaintext = rsa.Decrypt(ciphertext.Span, RSAEncryptionPadding.OaepSHA256);
            return Task.FromResult(plaintext);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new KrypteiaException("Decryption failed.", ex);
        }
    }
}
