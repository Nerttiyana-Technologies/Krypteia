using Krypteia.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Krypteia.EntityFrameworkCore;

/// <summary>
/// Database-backed implementation of <see cref="IResetTokenStore"/> using EF Core.
/// </summary>
/// <remarks>
/// <para>
/// Tokens survive process restarts, which is what users expect. Consumers
/// using Redis or another transient store can implement
/// <see cref="IResetTokenStore"/> against their cache instead — tokens are
/// short-lived enough that ephemeral storage is acceptable, though crashes
/// mid-flow will force users to request a new reset.
/// </para>
/// <para>
/// <b>Cross-provider note.</b> SQLite cannot translate <see cref="DateTimeOffset"/>
/// comparisons into SQL (it stores them as text and has no native operators
/// for them). To stay compatible with all four target providers — SQL Server,
/// PostgreSQL, MariaDB, SQLite — date filtering on <c>CreatedAt</c> happens
/// in memory after a narrow query has fetched the candidate rows. Since rate
/// limits operate on a single user's recent history (default: last hour),
/// the candidate set is tiny.
/// </para>
/// </remarks>
public sealed class DbResetTokenStore : IResetTokenStore
{
    private readonly KrypteiaDbContext _db;

    /// <summary>Initializes a new instance.</summary>
    public DbResetTokenStore(KrypteiaDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <inheritdoc />
    public async Task StoreAsync(
        ResetTokenRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        _db.ResetTokens.Add(new ResetTokenEntity
        {
            TokenHash = record.TokenHash,
            UserId = record.UserId,
            CreatedAt = record.CreatedAt,
            ExpiresAt = record.ExpiresAt,
            UsedAt = record.UsedAt,
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ResetTokenRecord?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        ResetTokenEntity? entity = await _db.ResetTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        return new ResetTokenRecord(
            TokenHash: entity.TokenHash,
            UserId: entity.UserId,
            CreatedAt: entity.CreatedAt,
            ExpiresAt: entity.ExpiresAt,
            UsedAt: entity.UsedAt);
    }

    /// <inheritdoc />
    public async Task MarkUsedAsync(
        string tokenHash,
        DateTimeOffset usedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        ResetTokenEntity? entity = await _db.ResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            // Idempotent: marking a non-existent token used is a no-op, not
            // an error. This matches the interface contract.
            return;
        }

        // Idempotent: if already used, leave the original UsedAt alone so
        // the audit trail shows the first use, not the most recent attempt.
        if (entity.UsedAt is not null)
        {
            return;
        }

        entity.UsedAt = usedAt;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> CountRecentAttemptsAsync(
        string userId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // SQLite cannot translate DateTimeOffset comparisons to SQL, so we
        // fetch this user's rows (cheap — there are at most a handful per
        // user, bounded by the rate limit) and apply the date filter in
        // memory. SQL-translatable filter on UserId only.
        List<DateTimeOffset> createdAts = await _db.ResetTokens
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => t.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return createdAts.Count(c => c >= since);
    }
}