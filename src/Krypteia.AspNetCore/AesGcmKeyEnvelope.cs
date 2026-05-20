using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Krypteia.Abstractions;

namespace Krypteia;

/// <summary>
/// AES-256-GCM implementation of <see cref="IKeyEnvelope"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Algorithm:</b> AES-256 in Galois/Counter Mode (GCM), providing both
/// confidentiality and authenticity. NIST SP 800-38D.
/// </para>
/// <para>
/// <b>Wire format:</b> the output of <see cref="WrapAsync"/> is a Base64-encoded
/// concatenation of:
/// </para>
/// <code>
/// [1 byte version = 0x01] [12 bytes nonce] [16 bytes tag] [N bytes ciphertext]
/// </code>
/// <para>
/// The version byte allows future migration to a different format without
/// breaking existing records.
/// </para>
/// <para>
/// <b>Nonce reuse:</b> each call generates a fresh random nonce. The
/// probability of collision across 2^32 wraps under the same master key is
/// negligible (~2^-32), which is well within NIST's recommendations for
/// random nonces with AES-GCM.
/// </para>
/// </remarks>
public sealed class AesGcmKeyEnvelope : IKeyEnvelope
{
    private const byte WireFormatVersion = 0x01;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int MasterKeySize = 32; // AES-256

    private readonly IMasterKeyProvider _masterKeys;

    /// <summary>Initializes a new instance.</summary>
    public AesGcmKeyEnvelope(IMasterKeyProvider masterKeys)
    {
        _masterKeys = masterKeys ?? throw new ArgumentNullException(nameof(masterKeys));
    }

    /// <inheritdoc />
    public async Task<string> WrapAsync(
        string privateKeyPem,
        string masterKeyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(privateKeyPem);
        ArgumentNullException.ThrowIfNull(masterKeyId);
        cancellationToken.ThrowIfCancellationRequested();

        byte[]? masterKey = null;
        byte[]? plaintext = null;
        try
        {
            masterKey = await _masterKeys.GetMasterKeyAsync(masterKeyId, cancellationToken).ConfigureAwait(false);
            EnsureKeySize(masterKey);

            plaintext = Encoding.UTF8.GetBytes(privateKeyPem);

            byte[] nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[TagSize];

            using (var aes = new AesGcm(masterKey, TagSize))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag);
            }

            // Build wire format: [version][nonce][tag][ciphertext]
            byte[] envelope = new byte[1 + NonceSize + TagSize + ciphertext.Length];
            envelope[0] = WireFormatVersion;
            Buffer.BlockCopy(nonce, 0, envelope, 1, NonceSize);
            Buffer.BlockCopy(tag, 0, envelope, 1 + NonceSize, TagSize);
            Buffer.BlockCopy(ciphertext, 0, envelope, 1 + NonceSize + TagSize, ciphertext.Length);

            return Convert.ToBase64String(envelope);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not KrypteiaException)
        {
            throw new KrypteiaException("Key wrap failed.", ex);
        }
        finally
        {
            if (masterKey is not null) CryptographicOperations.ZeroMemory(masterKey);
            if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <inheritdoc />
    public async Task<string> UnwrapAsync(
        string wrappedKey,
        string masterKeyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wrappedKey);
        ArgumentNullException.ThrowIfNull(masterKeyId);
        cancellationToken.ThrowIfCancellationRequested();

        byte[]? masterKey = null;
        byte[]? plaintext = null;
        try
        {
            byte[] envelope = Convert.FromBase64String(wrappedKey);

            if (envelope.Length < 1 + NonceSize + TagSize)
            {
                throw new KrypteiaException("Wrapped key blob is malformed.");
            }

            if (envelope[0] != WireFormatVersion)
            {
                throw new KrypteiaException("Unsupported wrapped key format version.");
            }

            masterKey = await _masterKeys.GetMasterKeyAsync(masterKeyId, cancellationToken).ConfigureAwait(false);
            EnsureKeySize(masterKey);

            int ciphertextLength = envelope.Length - 1 - NonceSize - TagSize;

            ReadOnlySpan<byte> nonce = envelope.AsSpan(1, NonceSize);
            ReadOnlySpan<byte> tag = envelope.AsSpan(1 + NonceSize, TagSize);
            ReadOnlySpan<byte> ciphertext = envelope.AsSpan(1 + NonceSize + TagSize, ciphertextLength);

            plaintext = new byte[ciphertextLength];

            using (var aes = new AesGcm(masterKey, TagSize))
            {
                // AuthenticationTagMismatchException is thrown if tag verification fails.
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
            }

            return Encoding.UTF8.GetString(plaintext);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not KrypteiaException)
        {
            // Generic message — never distinguish between "wrong master key"
            // and "tampered ciphertext", same reason as the RSA path.
            throw new KrypteiaException("Key unwrap failed.", ex);
        }
        finally
        {
            if (masterKey is not null) CryptographicOperations.ZeroMemory(masterKey);
            if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static void EnsureKeySize(byte[] key)
    {
        if (key.Length != MasterKeySize)
        {
            throw new KrypteiaException(
                $"Master key must be {MasterKeySize} bytes (256 bits); got {key.Length}.");
        }

        // Suppress unused-variable warning on the buffer used for length check.
        _ = BinaryPrimitives.ReadInt32LittleEndian(key.AsSpan(0, 4));
    }
}