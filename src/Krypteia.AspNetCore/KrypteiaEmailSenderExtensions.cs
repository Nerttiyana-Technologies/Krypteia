using Krypteia.Abstractions;
using Krypteia.KeyReset;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krypteia.AspNetCore;

/// <summary>
/// DI extensions for registering a Krypteia email sender from configuration.
/// </summary>
public static class KrypteiaEmailSenderExtensions
{
    /// <summary>
    /// Registers an <see cref="IEmailSender"/> based on the bound
    /// <see cref="EmailSenderOptions"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Handles the <see cref="EmailProvider.Console"/> and
    /// <see cref="EmailProvider.Smtp"/> providers, both of which live in
    /// <c>Krypteia.KeyReset</c>.
    /// </para>
    /// <para>
    /// <b>SendGrid is intentionally not handled here.</b> The SendGrid sender
    /// lives in the optional <c>Krypteia.EmailSender.SendGrid</c> package so
    /// that consumers who don't use SendGrid never take its dependency. If
    /// <see cref="EmailSenderOptions.Provider"/> is
    /// <see cref="EmailProvider.SendGrid"/>, call
    /// <c>AddKrypteiaSendGridEmailSender()</c> from that package instead of
    /// (or in addition to) this method.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration root; the <c>"EmailSender"</c> section is bound.</param>
    /// <param name="sectionName">The configuration section name. Defaults to <c>"EmailSender"</c>.</param>
    public static IServiceCollection AddKrypteiaEmailSender(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "EmailSender")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section = configuration.GetSection(sectionName);
        services.Configure<EmailSenderOptions>(section);

        // Read the provider value now, so we can register the right type.
        var options = new EmailSenderOptions();
        section.Bind(options);

        switch (options.Provider)
        {
            case EmailProvider.Console:
                services.TryAddScoped<IEmailSender, ConsoleEmailSender>();
                break;

            case EmailProvider.Smtp:
                services.TryAddScoped<IEmailSender, SmtpEmailSender>();
                break;

            case EmailProvider.SendGrid:
                // The SendGrid sender is in a separate optional package.
                // We cannot reference it from here without forcing the
                // dependency on everyone. Fail loudly with a clear message.
                throw new InvalidOperationException(
                    "EmailSender:Provider is set to 'SendGrid', but the SendGrid sender " +
                    "is in the optional Krypteia.EmailSender.SendGrid package. Add a " +
                    "reference to that package and call AddKrypteiaSendGridEmailSender() " +
                    "instead of AddKrypteiaEmailSender().");

            default:
                throw new InvalidOperationException(
                    $"Unknown EmailSender:Provider value '{options.Provider}'.");
        }

        return services;
    }
}