using AwesomeAssertions;
using Krypteia.Abstractions;
using Krypteia.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Krypteia.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="DbResetTokenStore"/> against a real
/// in-memory SQLite database.
/// </summary>
/// <remarks>
/// <para>
/// These tests are the safety net against provider-specific LINQ translation
/// issues. SQLite cannot translate <see cref="DateTimeOffset"/> comparisons,
/// for example, and the only way to catch that is to run real queries through
/// a real provider. Mocking <see cref="KrypteiaDbContext"/> would have hidden
/// the problem.
/// </para>
/// <para>
/// Each test gets its own freshly-opened SQLite <c>:memory:</c> database via
/// the <see cref="IDisposable"/> pattern. The connection is kept open for the
/// lifetime of the test because <c>:memory:</c> databases vanish when the
/// last connection is closed.
/// </para>
/// </remarks>
public class DbResetTokenStoreTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<KrypteiaDbContext> _options;

    public DbResetTokenStoreTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<KrypteiaDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new KrypteiaDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private KrypteiaDbContext CreateContext() => new(_options);

    private DbResetTokenStore CreateSut() => new(CreateContext());

    [Fact]
    public async Task StoreAsync_PersistsRecord()
    {
        DbResetTokenStore sut = CreateSut();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var record = new ResetTokenRecord(
            TokenHash: "hash-1",
            UserId: "alice",
            CreatedAt: now,
            ExpiresAt: now + TimeSpan.FromMinutes(15),
            UsedAt: null);

        await sut.StoreAsync(record);

        // Verify with a fresh context that the row actually landed in the DB.
        await using var verifier = CreateContext();
        ResetTokenEntity? entity = await verifier.ResetTokens.FirstOrDefaultAsync(t => t.TokenHash == "hash-1");
        entity.Should().NotBeNull();
        entity!.UserId.Should().Be("alice");
    }

    [Fact]
    public async Task FindByHashAsync_ForExistingToken_ReturnsRecord()
    {
        DbResetTokenStore sut = CreateSut();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await sut.StoreAsync(new ResetTokenRecord(
            "hash-known", "alice", now, now + TimeSpan.FromMinutes(15), null));

        ResetTokenRecord? found = await sut.FindByHashAsync("hash-known");

        found.Should().NotBeNull();
        found!.UserId.Should().Be("alice");
    }

    [Fact]
    public async Task FindByHashAsync_ForUnknownToken_ReturnsNull()
    {
        DbResetTokenStore sut = CreateSut();

        ResetTokenRecord? found = await sut.FindByHashAsync("hash-that-does-not-exist");

        found.Should().BeNull();
    }

    [Fact]
    public async Task MarkUsedAsync_UpdatesUsedAtTimestamp()
    {
        DbResetTokenStore sut = CreateSut();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await sut.StoreAsync(new ResetTokenRecord(
            "hash-to-use", "alice", now, now + TimeSpan.FromMinutes(15), null));

        DateTimeOffset usedAt = now + TimeSpan.FromMinutes(1);
        await sut.MarkUsedAsync("hash-to-use", usedAt);

        ResetTokenRecord? after = await sut.FindByHashAsync("hash-to-use");
        after.Should().NotBeNull();
        after!.UsedAt.Should().BeCloseTo(usedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task MarkUsedAsync_OnAlreadyUsedToken_DoesNotOverwriteOriginalTimestamp()
    {
        DbResetTokenStore sut = CreateSut();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset firstUse = now + TimeSpan.FromMinutes(1);

        await sut.StoreAsync(new ResetTokenRecord(
            "hash-double-use", "alice", now, now + TimeSpan.FromMinutes(15), null));

        await sut.MarkUsedAsync("hash-double-use", firstUse);
        await sut.MarkUsedAsync("hash-double-use", now + TimeSpan.FromMinutes(5));

        ResetTokenRecord? after = await sut.FindByHashAsync("hash-double-use");
        after!.UsedAt.Should().BeCloseTo(firstUse, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task MarkUsedAsync_OnUnknownToken_DoesNotThrow()
    {
        // Idempotency: marking a non-existent token as used is a no-op.
        DbResetTokenStore sut = CreateSut();

        Func<Task> act = () => sut.MarkUsedAsync("hash-not-found", DateTimeOffset.UtcNow);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CountRecentAttemptsAsync_ReturnsCorrectCount()
    {
        // This is the test that would have caught the DateTimeOffset SQLite
        // translation issue. If the LINQ query can't be translated, this throws.
        DbResetTokenStore sut = CreateSut();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Two recent attempts for alice, one ancient one for alice, one for bob.
        await sut.StoreAsync(new ResetTokenRecord("hash-a1", "alice", now - TimeSpan.FromMinutes(30), now, null));
        await sut.StoreAsync(new ResetTokenRecord("hash-a2", "alice", now - TimeSpan.FromMinutes(10), now, null));
        await sut.StoreAsync(new ResetTokenRecord("hash-a3", "alice", now - TimeSpan.FromDays(7), now, null));
        await sut.StoreAsync(new ResetTokenRecord("hash-b1", "bob", now - TimeSpan.FromMinutes(5), now, null));

        // "Recent" = last hour.
        int aliceRecent = await sut.CountRecentAttemptsAsync("alice", now - TimeSpan.FromHours(1));
        int bobRecent = await sut.CountRecentAttemptsAsync("bob", now - TimeSpan.FromHours(1));

        aliceRecent.Should().Be(2, "two of alice's three attempts are within the last hour");
        bobRecent.Should().Be(1);
    }

    [Fact]
    public async Task CountRecentAttemptsAsync_WithNoAttempts_ReturnsZero()
    {
        DbResetTokenStore sut = CreateSut();

        int count = await sut.CountRecentAttemptsAsync("ghost", DateTimeOffset.UtcNow - TimeSpan.FromHours(1));

        count.Should().Be(0);
    }

    [Fact]
    public async Task StoreAsync_WithDuplicateHash_Throws()
    {
        // TokenHash is the primary key. Insertions with a duplicate hash
        // must fail loudly — silent overwrite would let an attacker who
        // could predict hashes hijack a session.
        //
        // Two separate stores (each with its own DbContext) so the duplicate
        // insert reaches the database rather than the in-memory change
        // tracker. That's the path that exercises the real uniqueness
        // constraint — what we actually want to verify.
        DbResetTokenStore first = CreateSut();
        DbResetTokenStore second = CreateSut();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await first.StoreAsync(new ResetTokenRecord(
            "hash-dup", "alice", now, now + TimeSpan.FromMinutes(15), null));

        Func<Task> act = () => second.StoreAsync(
            new ResetTokenRecord("hash-dup", "alice", now, now + TimeSpan.FromMinutes(15), null));

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}