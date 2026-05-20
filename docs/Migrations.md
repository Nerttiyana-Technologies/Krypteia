# Database Migrations

Krypteia stores user key material and reset tokens in a relational database via
Entity Framework Core. This document explains how to set up and evolve the
database schema in an application that uses Krypteia.

## Krypteia does not ship migrations

Krypteia supports four database providers — SQL Server, PostgreSQL, MariaDB, and
SQLite. EF Core migrations are **provider-specific**: a migration generated
against SQLite contains SQLite-flavored SQL that will not run on PostgreSQL, and
vice versa.

Because of that, Krypteia deliberately ships **no migration files**. The
`KrypteiaDbContext` and its model live in the `Krypteia.EntityFrameworkCore`
library, but migrations are owned by **your application**, generated against
whichever provider you actually deploy on.

The sample app (`samples/Krypteia.Samples.WebApi`) demonstrates the full
setup with SQLite. Use it as a reference; do not attempt to reuse its
migration files for a different provider.

## One-time setup

### 1. Reference the EF Core design package

Add this to your application's project file (the project that runs and
configures the `DbContext` — not a class library):

    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>

`PrivateAssets=all` marks it as design-time tooling so it does not flow as a
dependency to anything that references your app.

### 2. Reference your database provider

Add the EF Core provider package for your database. One of:

    Microsoft.EntityFrameworkCore.SqlServer      (SQL Server / Azure SQL)
    Npgsql.EntityFrameworkCore.PostgreSQL         (PostgreSQL)
    Pomelo.EntityFrameworkCore.MySql              (MariaDB / MySQL)
    Microsoft.EntityFrameworkCore.Sqlite          (SQLite)

### 3. Tell EF Core the migrations assembly

By default EF Core expects migrations to live in the same assembly as the
`DbContext` — which for Krypteia is the library, where you do not want them.
Direct migrations into your application's assembly with `MigrationsAssembly`:

    builder.Services.AddDbContext<KrypteiaDbContext>(options =>
        options.UseNpgsql(
            connectionString,
            b => b.MigrationsAssembly("YourApp")));

Replace `UseNpgsql` with the call for your provider, and `"YourApp"` with the
name of your application's assembly.

### 4. Install the EF Core CLI tool

    dotnet tool install --global dotnet-ef

Keep it on the same version as your EF Core packages:

    dotnet tool update --global dotnet-ef
    dotnet ef --version

## Generating the initial migration

From the solution root:

    dotnet ef migrations add InitialCreate \
      --project YourApp \
      --startup-project YourApp

`--project` is where the migration files are written. `--startup-project` is
the app EF Core runs to discover the configured context. For a typical web app
both are the same project.

This creates a `Migrations/` folder with two files: the migration itself
(`<timestamp>_InitialCreate.cs`) and the model snapshot
(`KrypteiaDbContextModelSnapshot.cs`). Commit both to source control.

### Relax analyzers for the Migrations folder

EF Core's migration generator emits code that does not satisfy every analyzer
rule (for example, `CA1861` on inline constant arrays). If your project uses
strict analysis with warnings-as-errors, add an `.editorconfig` inside the
`Migrations/` folder:

    [*.cs]
    generated_code = true
    dotnet_diagnostic.CA1861.severity = none
    dotnet_diagnostic.CS1591.severity = none

`generated_code = true` relaxes a whole class of rules for generated files,
so a future regenerated migration will not reintroduce a build break.

## Applying migrations at startup

Call `Database.MigrateAsync()` during application startup to apply any pending
migrations:

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<KrypteiaDbContext>();
        await db.Database.MigrateAsync();
    }

`MigrateAsync()` creates the schema on a fresh database, applies any pending
migrations on an existing one, and does nothing if the database is already
current.

For production you may prefer to apply migrations as a separate deploy step
(`dotnet ef database update`) rather than at application startup, so that
multiple app instances do not race to migrate. Both approaches are valid;
choose based on your deployment model.

## Migrating a database created before migrations were adopted

If your application previously created its schema with
`Database.EnsureCreatedAsync()`, that database has the Krypteia tables but **no
`__EFMigrationsHistory` table**. Calling `MigrateAsync()` on it directly fails:
EF Core sees no migration history, tries to run `InitialCreate` from scratch,
and errors because the tables already exist.

The fix is to **baseline** the database — record `InitialCreate` as already
applied without re-running its SQL.

The sample app's `KrypteiaDatabaseInitializer` class shows one way to do this
in code: it detects a populated database with no migration history, writes the
`__EFMigrationsHistory` table and the baseline row, then proceeds with
`MigrateAsync()`. See `samples/Krypteia.Samples.WebApi/KrypteiaDatabaseInitializer.cs`.

You can also baseline manually. With the application stopped:

1. Generate the SQL for the initial migration:

       dotnet ef migrations script --output initial.sql

2. From `initial.sql`, run **only** the statements that create the
   `__EFMigrationsHistory` table and insert the row for `InitialCreate`.
   Do not run the `CREATE TABLE` statements for the Krypteia tables — they
   already exist.

After baselining, `MigrateAsync()` behaves normally and future migrations
apply cleanly.

## Adding a migration when the schema changes

When a future version of Krypteia changes the `DbContext` model, you generate
a new migration the same way:

    dotnet ef migrations add <DescriptiveName> \
      --project YourApp \
      --startup-project YourApp

Review the generated migration, commit it, and it applies on the next
`MigrateAsync()` or `dotnet ef database update`.

## Provider-specific notes

- **SQLite** has limited `ALTER TABLE` support. Some schema changes that are
  simple on other providers require EF Core to rebuild the table. This is
  handled automatically but can be slow on large tables.
- **SQLite** cannot translate `DateTimeOffset` comparisons in LINQ queries.
  Krypteia's own queries are written to avoid this, but be aware of it in
  your own code against `KrypteiaDbContext`.
- **PostgreSQL, SQL Server, MariaDB** all support the full range of schema
  operations without special handling.

## Summary

- Krypteia ships no migrations; your application owns them.
- Generate migrations against the provider you deploy on.
- Point `MigrationsAssembly` at your application's assembly.
- Relax analyzers for the generated `Migrations/` folder.
- Baseline any database that predates migrations before calling `MigrateAsync()`.