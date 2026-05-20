using Krypteia.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Krypteia.Samples.WebApi;

/// <summary>
/// Brings the Krypteia database schema up to date at application startup,
/// safely handling databases that predate the use of EF Core migrations.
/// </summary>
/// <remarks>
/// <para>
/// Earlier versions of this sample created the schema with
/// <c>Database.EnsureCreatedAsync()</c>, which builds tables directly and
/// does <b>not</b> record anything in the <c>__EFMigrationsHistory</c> table.
/// A database created that way cannot simply be handed to
/// <c>Database.MigrateAsync()</c> — EF Core would see an empty migration
/// history, attempt to run <c>InitialCreate</c> from scratch, and fail
/// because the tables already exist.
/// </para>
/// <para>
/// This initializer detects that situation and "baselines" the database:
/// it records <c>InitialCreate</c> as already applied without re-running
/// its SQL, then proceeds normally.
/// </para>
/// <para>
/// This logic belongs in the <b>consuming application</b>, not the Krypteia
/// library — different applications have different startup and deployment
/// models. The sample demonstrates one reasonable approach.
/// </para>
/// </remarks>
internal static partial class KrypteiaDatabaseInitializer
{
    /// <summary>
    /// The trailing name of the first migration. Used as the baseline marker
    /// for databases created before migrations were adopted.
    /// </summary>
    private const string InitialMigrationId = "InitialCreate";

    /// <summary>
    /// Ensures the database schema is current, baselining a pre-migrations
    /// database if one is detected.
    /// </summary>
    public static async Task InitializeAsync(
        KrypteiaDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);

        bool databaseExists = await db.Database.CanConnectAsync(cancellationToken)
            .ConfigureAwait(false);

        if (databaseExists)
        {
            IEnumerable<string> applied = await db.Database
                .GetAppliedMigrationsAsync(cancellationToken)
                .ConfigureAwait(false);

            bool hasMigrationHistory = applied.Any();

            bool krypteiaTablesExist = await KrypteiaTablesExistAsync(db, cancellationToken)
                .ConfigureAwait(false);

            if (!hasMigrationHistory && krypteiaTablesExist)
            {
                Log.BaseliningDatabase(logger);
                await BaselineAsync(db, cancellationToken).ConfigureAwait(false);
            }
        }

        // Brand new, freshly baselined, or already migrations-managed —
        // MigrateAsync does the right thing in all three cases.
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        Log.DatabaseReady(logger);
    }

    /// <summary>
    /// Probes for a known Krypteia table to decide whether this is a
    /// populated pre-migrations database.
    /// </summary>
    private static async Task<bool> KrypteiaTablesExistAsync(
        KrypteiaDbContext db,
        CancellationToken cancellationToken)
    {
        // SQLite-specific probe (the sample uses SQLite). A multi-provider
        // app would branch on the provider here. Documented in docs/MIGRATIONS.md.
        const string sql =
            "SELECT COUNT(*) FROM sqlite_master " +
            "WHERE type = 'table' AND name = 'KrypteiaUserKeys';";

        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        await db.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            object? result = await command.ExecuteScalarAsync(cancellationToken)
                .ConfigureAwait(false);
            return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture) > 0;
        }
        finally
        {
            await db.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Records <see cref="InitialMigrationId"/> as applied without running
    /// its SQL.
    /// </summary>
    private static async Task BaselineAsync(
        KrypteiaDbContext db,
        CancellationToken cancellationToken)
    {
        var historyRepository = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IHistoryRepository>();

        string createHistoryTableSql = historyRepository.GetCreateIfNotExistsScript();
        await db.Database.ExecuteSqlRawAsync(createHistoryTableSql, cancellationToken)
            .ConfigureAwait(false);

        string? initialMigrationId = db.Database
            .GetMigrations()
            .FirstOrDefault(m => m.EndsWith(InitialMigrationId, StringComparison.Ordinal));

        if (initialMigrationId is null)
        {
            throw new InvalidOperationException(
                $"Could not find a migration ending in '{InitialMigrationId}'. " +
                "The migrations assembly may be misconfigured.");
        }

        string insertSql = historyRepository.GetInsertScript(
            new Microsoft.EntityFrameworkCore.Migrations.HistoryRow(
                initialMigrationId,
                ProductInfo.GetVersion()));

        await db.Database.ExecuteSqlRawAsync(insertSql, cancellationToken)
            .ConfigureAwait(false);
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 4000,
            Level = LogLevel.Warning,
            Message =
                "Existing Krypteia tables found with no migration history. " +
                "Baselining the database: recording InitialCreate as applied " +
                "without re-running it.")]
        public static partial void BaseliningDatabase(ILogger logger);

        [LoggerMessage(
            EventId = 4001,
            Level = LogLevel.Information,
            Message = "Krypteia database schema is up to date.")]
        public static partial void DatabaseReady(ILogger logger);
    }
}