using Krypteia.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Krypteia.EntityFrameworkCore;

/// <summary>
/// EF Core-backed implementation of <see cref="IKeyManagementService"/>.
/// </summary>
/// <remarks>
/// <para>
/// This service is the production-ready home for user key material. It:
/// </para>
/// <list type="bullet">
///   <item><description>Generates RSA-2048 key pairs using <see cref="RsaKeyPairGenerator"/></description></item>
///   <item><description>Wraps the private key with the current master key via <see cref="IKeyEnvelope"/></description></item>
///   <item><description>Persists the public key and the wrapped backup via <see cref="KrypteiaDbContext"/></description></item>
///   <item><description>Returns the freshly generated private key to the caller exactly once; from that moment on, the server cannot recover it without the master key</description></item>
/// </list>
/// <para>
/// Works with SQL Server, PostgreSQL, MariaDB, and SQLite — the schema uses
/// only plain types and EF Core handles provider differences.
/// </para>
/// </remarks>
public sealed class EfCoreKeyManagementService : IKeyManagementService
{
    private readonly KrypteiaDbContext _db;
    private readonly IKeyEnvelope _envelope;
    private readonly IMasterKeyProvider _masterKeys;

    /// <summary>Initializes a new instance.</summary>
    public EfCoreKeyManagementService(
        KrypteiaDbContext db,
        IKeyEnvelope envelope,
        IMasterKeyProvider masterKeys)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
        _masterKeys = masterKeys ?? throw new ArgumentNullException(nameof(masterKeys));
    }

    /// <inheritdoc />
    public async Task<KeyPair> GenerateKeyPairAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // Determine version: existing user → bump; new user → version 1.
        UserKeyEntity? existing = await _db.UserKeys
            .FirstOrDefaultAsync(k => k.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        int newVersion = existing is null ? 1 : existing.KeyVersion + 1;

        // Generate the RSA key pair. The private key exists only in memory
        // for the duration of this method.
        KeyPair pair = RsaKeyPairGenerator.Generate(version: newVersion);

        // Wrap the private key with the current master key.
        string masterKeyId = _masterKeys.CurrentKeyId;
        string wrapped = await _envelope.WrapAsync(pair.PrivateKey, masterKeyId, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            _db.UserKeys.Add(new UserKeyEntity
            {
                UserId = userId,
                PublicKey = pair.PublicKey,
                EncryptedPrivateKeyBackup = wrapped,
                KeyVersion = newVersion,
                MasterKeyId = masterKeyId,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.PublicKey = pair.PublicKey;
            existing.EncryptedPrivateKeyBackup = wrapped;
            existing.KeyVersion = newVersion;
            existing.MasterKeyId = masterKeyId;
            existing.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Return the in-memory key pair to the caller. After this point the
        // server has no plaintext copy of the private key — it lives only in
        // the encrypted backup column and (transitively) in whatever the
        // caller chooses to do with the returned KeyPair.
        return pair;
    }

    /// <inheritdoc />
    public async Task<string?> GetPublicKeyAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        UserKeyEntity? entity = await _db.UserKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        return entity?.PublicKey;
    }

    /// <inheritdoc />
    public async Task StorePublicKeyAsync(
        string userId,
        string publicKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(publicKey);

        UserKeyEntity? existing = await _db.UserKeys
            .FirstOrDefaultAsync(k => k.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            // No row yet — create a placeholder with the public key. The
            // encrypted private key backup column is required by the schema,
            // so we mark it as empty until a key generation actually populates it.
            // Applications using StorePublicKeyAsync for client-generated keys
            // (where the server never sees the private key) should be aware
            // that the reset flow will not work for such users.
            _db.UserKeys.Add(new UserKeyEntity
            {
                UserId = userId,
                PublicKey = publicKey,
                EncryptedPrivateKeyBackup = string.Empty,
                KeyVersion = 1,
                MasterKeyId = _masterKeys.CurrentKeyId,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.PublicKey = publicKey;
            existing.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> GetKeyVersionAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        UserKeyEntity? entity = await _db.UserKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        return entity?.KeyVersion ?? 0;
    }

    /// <inheritdoc />
    public async Task<EncryptedKeyBackup?> GetEncryptedBackupAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        UserKeyEntity? entity = await _db.UserKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null || string.IsNullOrEmpty(entity.EncryptedPrivateKeyBackup))
        {
            return null;
        }

        return new EncryptedKeyBackup(
            WrappedPrivateKey: entity.EncryptedPrivateKeyBackup,
            MasterKeyId: entity.MasterKeyId,
            KeyVersion: entity.KeyVersion);
    }
}