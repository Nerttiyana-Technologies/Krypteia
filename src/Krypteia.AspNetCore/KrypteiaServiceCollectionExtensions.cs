using Krypteia.Abstractions;
using Krypteia.EntityFrameworkCore;
using Krypteia.KeyReset;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krypteia.AspNetCore;

/// <summary>
/// Dependency injection extensions for registering Krypteia services in an
/// ASP.NET Core application.
/// </summary>
public static class KrypteiaServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core Krypteia encryption service (<see cref="IEncryptionService"/>).
    /// </summary>
    /// <remarks>
    /// This is the minimum registration needed to call encryption and decryption.
    /// For key storage and the reset flow, also call <see cref="AddKrypteiaPersistence{TContext}"/>
    /// and <see cref="AddFileMasterKeyProvider"/>.
    /// </remarks>
    public static IServiceCollection AddKrypteia(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IEncryptionService, RsaEncryptionService>();

        return services;
    }

    /// <summary>
    /// Registers the file-backed <see cref="IMasterKeyProvider"/>. Suitable for
    /// development and single-server deployments. For production multi-server
    /// scenarios, register a Key Vault / KMS / Vault-backed implementation instead.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the directory and current key identifier.</param>
    public static IServiceCollection AddFileMasterKeyProvider(
        this IServiceCollection services,
        Action<FileMasterKeyProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new FileMasterKeyProviderOptions();
        configure(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IMasterKeyProvider>(sp =>
            new FileMasterKeyProvider(sp.GetRequiredService<FileMasterKeyProviderOptions>()));

        services.TryAddSingleton<IKeyEnvelope, AesGcmKeyEnvelope>();

        return services;
    }

    /// <summary>
    /// Registers EF Core-backed key management against the supplied
    /// <typeparamref name="TContext"/>. The context must inherit from
    /// <see cref="KrypteiaDbContext"/> or be the type itself.
    /// </summary>
    /// <typeparam name="TContext">The application's DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <example>
    /// <code>
    /// builder.Services.AddDbContext&lt;KrypteiaDbContext&gt;(o =&gt; o.UseSqlite("Data Source=krypteia.db"));
    /// builder.Services.AddKrypteia()
    ///     .AddFileMasterKeyProvider(o =&gt; { o.Directory = "/var/lib/krypteia/keys"; o.CurrentKeyId = "v1"; })
    ///     .AddKrypteiaPersistence&lt;KrypteiaDbContext&gt;();
    /// </code>
    /// </example>
    /// <remarks>
    /// The DbContext itself must be registered separately via the standard
    /// <see cref="EntityFrameworkServiceCollectionExtensions.AddDbContext{TContext}(IServiceCollection, Action{DbContextOptionsBuilder}?, ServiceLifetime, ServiceLifetime)"/>
    /// method — Krypteia does not pick a provider for you.
    /// </remarks>
    public static IServiceCollection AddKrypteiaPersistence<TContext>(this IServiceCollection services)
        where TContext : KrypteiaDbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        if (typeof(TContext) != typeof(KrypteiaDbContext))
        {
            services.AddScoped<KrypteiaDbContext>(sp => sp.GetRequiredService<TContext>());
        }
        //services.AddScoped<KrypteiaDbContext>(sp => sp.GetRequiredService<TContext>());

        services.AddScoped<IKeyManagementService, EfCoreKeyManagementService>();

        return services;
    }

    /// <summary>
    /// Registers the Krypteia key reset flow: <see cref="IKeyResetService"/>,
    /// the database-backed <see cref="IResetTokenStore"/>, and a development
    /// <see cref="IEmailSender"/> that prints emails to console.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Production checklist:</b> replace the registered <see cref="IEmailSender"/>
    /// with a real implementation (SendGrid, SES, Postmark, etc.) and optionally
    /// register an <see cref="IDataReencryptionService"/> to re-encrypt the user's
    /// existing data on reset. Without the latter, old data becomes unreadable
    /// after a key reset.
    /// </para>
    /// <para>
    /// This method depends on <see cref="AddKrypteiaPersistence{TContext}"/>
    /// having already registered the DbContext bridge. Call them in this order:
    /// </para>
    /// <code>
    /// builder.Services.AddKrypteia()
    ///     .AddFileMasterKeyProvider(...)
    ///     .AddKrypteiaPersistence&lt;KrypteiaDbContext&gt;()
    ///     .AddKrypteiaKeyReset(o =&gt;
    ///     {
    ///         o.ResetUrlBase = "https://app.example.com/reset";
    ///         o.FromAddress = "no-reply@example.com";
    ///     });
    /// </code>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures token lifetime, rate limits, URL base, etc.</param>
    public static IServiceCollection AddKrypteiaKeyReset(
        this IServiceCollection services,
        Action<KeyResetOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Standard options binding so consumers can also configure via
        // appsettings.json + builder.Services.Configure<KeyResetOptions>(...).
        services.Configure(configure);

        // Token store — database-backed by default. Consumers can replace
        // this registration with Redis or similar before or after this call.
        services.TryAddScoped<IResetTokenStore, DbResetTokenStore>();

        // Email sender — console default for development. Consumers MUST
        // replace this in production. Using Add (not TryAdd) so that
        // ConsoleEmailSender is the fallback if nothing else is registered,
        // but a prior registration wins.
        services.TryAddScoped<IEmailSender, ConsoleEmailSender>();

        // The audit service is also needed. The library ships LoggerAuditService
        // as the default; consumers can replace it for SIEM integration.
        services.TryAddScoped<IAuditService, Krypteia.Audit.LoggerAuditService>();

        // The reset service itself.
        services.TryAddScoped<IKeyResetService, KeyResetService>();

        return services;
    }
}