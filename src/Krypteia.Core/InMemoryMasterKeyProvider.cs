using System.Collections.Concurrent;
using System.Security.Cryptography;
using Krypteia.Abstractions;

namespace Krypteia;

/// <summary>
/// In-memory <see cref="IMasterKeyProvider"/> for unit testing and short-lived
/// scenarios. Generates and holds key material in process memory only; nothing
/// is persisted to disk.
/// </summary>
/// <remarks>
/// Not suitable for production: keys are lost on process restart, which makes
/// all existing wrapped backups unrecoverable.
/// </remarks>
public sealed class InMemoryMasterKeyProvider : IMasterKeyProvider
{
    private const int KeySizeBytes = 32;

    private readonly ConcurrentDictionary<string, byte[]> _keys = new(StringComparer.Ordinal);
    private readonly string _currentKeyId;

    /// <summary>Initializes a new instance with a freshly generated key.</summary>
    /// <param name="currentKeyId">The identifier to assign to the generated key.</param>
    public InMemoryMasterKeyProvider(string currentKeyId = "v1")
    {
        _currentKeyId = currentKeyId ?? throw new ArgumentNullException(nameof(currentKeyId));

        byte[] key = new byte[KeySizeBytes];
        RandomNumberGenerator.Fill(key);
        _keys[_currentKeyId] = key;
    }

    /// <inheritdoc />
    public string CurrentKeyId => _currentKeyId;

    /// <inheritdoc />
    public Task<byte[]> GetMasterKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_keys.TryGetValue(keyId, out byte[]? key))
        {
            throw new KrypteiaException($"Master key not found: {keyId}");
        }

        // Return a copy so the caller's ZeroMemory doesn't wipe our stored key.
        byte[] copy = new byte[key.Length];
        Buffer.BlockCopy(key, 0, copy, 0, key.Length);
        return Task.FromResult(copy);
    }

    /// <summary>
    /// Adds an additional key under a custom identifier. Useful for testing
    /// key rotation scenarios.
    /// </summary>
    public void AddKey(string keyId, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(keyId);
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != KeySizeBytes)
        {
            throw new ArgumentException(
                $"Master key must be {KeySizeBytes} bytes; got {key.Length}.",
                nameof(key));
        }

        byte[] copy = new byte[key.Length];
        Buffer.BlockCopy(key, 0, copy, 0, key.Length);
        _keys[keyId] = copy;
    }
}