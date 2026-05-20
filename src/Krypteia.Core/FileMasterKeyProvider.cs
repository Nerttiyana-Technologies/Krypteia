using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Krypteia.Abstractions;

namespace Krypteia;

/// <summary>
/// Stores the master key as a file on the local filesystem. Suitable for
/// development and single-server deployments where the file can be protected
/// by OS-level permissions. <b>Not suitable for multi-server production
/// deployments</b> — use a Key Vault, KMS, or HSM-backed provider instead.
/// </summary>
/// <remarks>
/// <para>
/// On first use, if no key file exists in the configured directory, a new
/// 256-bit AES key is generated using <see cref="RandomNumberGenerator"/>
/// and written to <c>master-{version}.key</c>. On Unix, the file is
/// created with permissions 0600 (owner read/write only).
/// </para>
/// <para>
/// The current key identifier is the filename (without extension). To rotate
/// keys, create a new file with a higher version number and set
/// <see cref="FileMasterKeyProviderOptions.CurrentKeyId"/> accordingly. The
/// provider retains read access to older keys so existing backups can still
/// be decrypted.
/// </para>
/// </remarks>
public sealed class FileMasterKeyProvider : IMasterKeyProvider, IDisposable
{
    private const int KeySizeBytes = 32;
    private const string KeyFileExtension = ".key";

    private readonly FileMasterKeyProviderOptions _options;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    /// <summary>Initializes a new instance.</summary>
    public FileMasterKeyProvider(FileMasterKeyProviderOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.Directory))
        {
            throw new ArgumentException(
                "FileMasterKeyProviderOptions.Directory must be set.",
                nameof(options));
        }

        if (string.IsNullOrWhiteSpace(_options.CurrentKeyId))
        {
            throw new ArgumentException(
                "FileMasterKeyProviderOptions.CurrentKeyId must be set (e.g., \"v1\").",
                nameof(options));
        }
    }

    /// <inheritdoc />
    public string CurrentKeyId => _options.CurrentKeyId;

    /// <inheritdoc />
    public async Task<byte[]> GetMasterKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyId);
        ValidateKeyId(keyId);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        string path = Path.Combine(_options.Directory, keyId + KeyFileExtension);

        if (!File.Exists(path))
        {
            throw new KrypteiaException($"Master key not found: {keyId}");
        }

        byte[] key = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);

        if (key.Length != KeySizeBytes)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new KrypteiaException(
                $"Master key file is the wrong size; expected {KeySizeBytes} bytes.");
        }

        return key;
    }

    /// <summary>
    /// Ensures the directory exists and the current key file is present.
    /// Generates a new key on first run if necessary.
    /// </summary>
    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized) return;

            Directory.CreateDirectory(_options.Directory);
            SetDirectoryPermissions(_options.Directory);

            string path = Path.Combine(_options.Directory, _options.CurrentKeyId + KeyFileExtension);

            if (!File.Exists(path))
            {
                byte[] newKey = new byte[KeySizeBytes];
                try
                {
                    RandomNumberGenerator.Fill(newKey);
                    await File.WriteAllBytesAsync(path, newKey, cancellationToken).ConfigureAwait(false);
                    SetFilePermissions(path);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(newKey);
                }
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static void ValidateKeyId(string keyId)
    {
        // Defensive: prevent path traversal. KeyId should be a simple
        // identifier like "v1" or "2026-q1", never contain path separators.
        if (keyId.Contains('/', StringComparison.Ordinal)
            || keyId.Contains('\\', StringComparison.Ordinal)
            || keyId.Contains("..", StringComparison.Ordinal))
        {
            throw new KrypteiaException("Master key identifier contains illegal characters.");
        }
    }

    private static void SetDirectoryPermissions(string path)
    {
        // chmod 700 on Unix; on Windows the default ACL is acceptable for dev.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void SetFilePermissions(string path)
    {
        // chmod 600 on Unix.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _initLock.Dispose();
    }
}

/// <summary>
/// Configuration for <see cref="FileMasterKeyProvider"/>.
/// </summary>
public sealed class FileMasterKeyProviderOptions
{
    /// <summary>
    /// The directory in which key files are stored. Must be readable and
    /// writable by the application's user account, and should not be world-readable.
    /// </summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>
    /// The identifier of the master key that should be used for new encryption
    /// operations. Corresponds to a file named <c>{CurrentKeyId}.key</c> in
    /// <see cref="Directory"/>. Example: <c>"v1"</c>.
    /// </summary>
    public string CurrentKeyId { get; set; } = "v1";
}