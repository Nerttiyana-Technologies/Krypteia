namespace Krypteia.Abstractions;

/// <summary>
/// Represents a user's RSA public/private key pair.
/// </summary>
/// <remarks>
/// <para>
/// This type intentionally overrides <see cref="ToString"/> to redact the private
/// key. Logging or serializing instances of this type with default settings will
/// not expose private key material.
/// </para>
/// <para>
/// Consumers that need to send the private key to the user must access the
/// <see cref="PrivateKey"/> property explicitly and zero out their buffer
/// after transmission.
/// </para>
/// </remarks>
public sealed class KeyPair
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KeyPair"/> class.
    /// </summary>
    /// <param name="publicKey">The public key in PEM format.</param>
    /// <param name="privateKey">The private key in PEM format. Treat as highly sensitive.</param>
    /// <param name="version">The key version. Used during reset to support multiple generations.</param>
    public KeyPair(string publicKey, string privateKey, int version = 1)
    {
        PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
        PrivateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
        Version = version;
    }

    /// <summary>
    /// Gets the public key in PEM format. Safe to log and persist on the server.
    /// </summary>
    public string PublicKey { get; }

    /// <summary>
    /// Gets the private key in PEM format. <b>Highly sensitive.</b>
    /// Must never be logged, serialized to disk, or persisted in plaintext.
    /// </summary>
    public string PrivateKey { get; }

    /// <summary>
    /// Gets the version number of this key pair. Increments on each reset.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// Returns a redacted string representation that never includes the private key.
    /// </summary>
    public override string ToString() =>
        $"KeyPair(Version={Version}, PublicKey={PublicKey[..Math.Min(40, PublicKey.Length)]}..., PrivateKey=[REDACTED])";
}
