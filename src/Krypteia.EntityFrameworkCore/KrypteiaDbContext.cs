using Microsoft.EntityFrameworkCore;

namespace Krypteia.EntityFrameworkCore;

/// <summary>
/// EF Core <see cref="DbContext"/> for Krypteia's persistence layer.
/// </summary>
/// <remarks>
/// <para>
/// Consumers register this context against their database provider of choice:
/// </para>
/// <code>
/// // SQL Server
/// services.AddDbContext&lt;KrypteiaDbContext&gt;(o =&gt; o.UseSqlServer(connectionString));
///
/// // PostgreSQL
/// services.AddDbContext&lt;KrypteiaDbContext&gt;(o =&gt; o.UseNpgsql(connectionString));
///
/// // MariaDB
/// services.AddDbContext&lt;KrypteiaDbContext&gt;(o =&gt; o.UseMySql(connectionString, serverVersion));
///
/// // SQLite (dev only)
/// services.AddDbContext&lt;KrypteiaDbContext&gt;(o =&gt; o.UseSqlite("Data Source=krypteia.db"));
/// </code>
/// <para>
/// Applications that want to keep Krypteia tables in their existing database
/// can inherit from this class and add their own <see cref="DbSet{TEntity}"/>
/// properties; <see cref="OnModelCreating"/> calls into
/// <see cref="ConfigureKrypteiaModel"/> which is also exposed for callers
/// who prefer to compose without inheritance.
/// </para>
/// </remarks>
public class KrypteiaDbContext : DbContext
{
    /// <summary>Initializes a new instance.</summary>
    public KrypteiaDbContext(DbContextOptions<KrypteiaDbContext> options)
        : base(options)
    {
    }

    /// <summary>Protected constructor for derived contexts that supply their own options type.</summary>
    protected KrypteiaDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>
    /// User key records. Contains one row per active user with key material.
    /// </summary>
    public DbSet<UserKeyEntity> UserKeys => Set<UserKeyEntity>();

    /// <summary>
    /// Reset token records. Contains one row per reset attempt — historical
    /// rows are retained for rate-limiting and audit purposes.
    /// </summary>
    public DbSet<ResetTokenEntity> ResetTokens => Set<ResetTokenEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
        ConfigureKrypteiaModel(modelBuilder);
    }

    /// <summary>
    /// Applies Krypteia's model configuration to the supplied <paramref name="modelBuilder"/>.
    /// Exposed as a static helper so applications that don't inherit from
    /// <see cref="KrypteiaDbContext"/> can still register Krypteia's entities
    /// inside their own context's <c>OnModelCreating</c>.
    /// </summary>
    public static void ConfigureKrypteiaModel(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<UserKeyEntity>(entity =>
        {
            entity.ToTable("KrypteiaUserKeys");

            entity.HasKey(e => e.UserId);

            entity.Property(e => e.UserId)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(e => e.PublicKey)
                .IsRequired();

            entity.Property(e => e.EncryptedPrivateKeyBackup)
                .IsRequired();

            entity.Property(e => e.MasterKeyId)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(e => e.KeyVersion)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .IsRequired();

            entity.HasIndex(e => e.MasterKeyId)
                .HasDatabaseName("IX_KrypteiaUserKeys_MasterKeyId");
        });

        modelBuilder.Entity<ResetTokenEntity>(entity =>
        {
            entity.ToTable("KrypteiaResetTokens");

            // The SHA-256 hex digest is 64 chars and uniquely identifies a
            // token, so it serves as the primary key. Lookups in the reset
            // flow are by this column.
            entity.HasKey(e => e.TokenHash);

            entity.Property(e => e.TokenHash)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(e => e.UserId)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.ExpiresAt)
                .IsRequired();

            // The rate limiter and audit queries both filter by UserId and a
            // date range; an index on (UserId, CreatedAt) makes both fast.
            entity.HasIndex(e => new { e.UserId, e.CreatedAt })
                .HasDatabaseName("IX_KrypteiaResetTokens_UserId_CreatedAt");
        });
    }
}