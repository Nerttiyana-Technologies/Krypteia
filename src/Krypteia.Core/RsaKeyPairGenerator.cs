using System.Security.Cryptography;
using Krypteia.Abstractions;

namespace Krypteia;

/// <summary>
/// Generates RSA key pairs in PEM format.
/// </summary>
/// <remarks>
/// Uses 2048-bit keys by default. This is the NIST-recommended minimum for new deployments
/// through 2030. For longer-lived data, callers may prefer 3072-bit or 4096-bit keys
/// at the cost of significantly slower key generation and encryption performance.
/// </remarks>
public static class RsaKeyPairGenerator
{
    /// <summary>The NIST-recommended minimum RSA key size for new deployments.</summary>
    public const int DefaultKeySize = 2048;

    /// <summary>
    /// Generates a new RSA key pair in PEM format.
    /// </summary>
    /// <param name="keySize">The RSA key size in bits. Must be at least 2048.</param>
    /// <param name="version">The version number to embed in the returned <see cref="KeyPair"/>.</param>
    /// <returns>A newly generated key pair.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="keySize"/> is below 2048 bits.
    /// </exception>
    public static KeyPair Generate(int keySize = DefaultKeySize, int version = 1)
    {
        if (keySize < 2048)
        {
            throw new ArgumentOutOfRangeException(
                nameof(keySize),
                keySize,
                "RSA key size must be at least 2048 bits.");
        }

        using var rsa = RSA.Create(keySize);

        string publicKey = rsa.ExportSubjectPublicKeyInfoPem();
        string privateKey = rsa.ExportPkcs8PrivateKeyPem();

        return new KeyPair(publicKey, privateKey, version);
    }
}
