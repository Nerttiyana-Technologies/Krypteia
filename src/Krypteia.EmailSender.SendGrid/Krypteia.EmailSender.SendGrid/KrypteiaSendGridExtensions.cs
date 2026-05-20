using Krypteia.Abstractions;
using Krypteia.KeyReset;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krypteia.EmailSender.SendGrid;

/// <summary>
/// DI extension for registering the SendGrid email sender.
/// </summary>
public static class KrypteiaSendGridExtensions
{
    /// <summary>
    /// Registers <see cref="SendGridEmailSender"/> as the <see cref="IEmailSender"/>.
    /// </summary>
    /// <remarks>
    /// Call this instead of <c>AddKrypteiaEmailSender</c> when
    /// <c>EmailSender:Provider</c> is <c>SendGrid</c>. This method also binds
    /// <see cref="EmailSenderOptions"/> from configuration, so you do not need
    /// to call <c>AddKrypteiaEmailSender</c> as well.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration root; the <c>"EmailSender"</c> section is bound.</param>
    /// <param name="sectionName">The configuration section name. Defaults to <c>"EmailSender"</c>.</param>
    public static IServiceCollection AddKrypteiaSendGridEmailSender(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "EmailSender")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<EmailSenderOptions>(configuration.GetSection(sectionName));
        services.TryAddScoped<IEmailSender, SendGridEmailSender>();

        return services;
    }
}