using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Romp.BuildingBlocks.Email;

public static class EmailServiceCollectionExtensions
{
    /// <summary>
    /// Idempotent: every module that sends email calls this, the first call wins. With <c>Email:Smtp:Host</c> set, SMTP is used and its
    /// settings are validated at start-up. Without it, Development and Testing record messages in a <see cref="FakeEmailSender"/>;
    /// any other environment refuses to resolve a sender, so production can never silently drop mail.
    /// </summary>
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration[$"{SmtpEmailOptions.Section}:Host"]))
        {
            services.AddOptions<SmtpEmailOptions>().Bind(configuration.GetSection(SmtpEmailOptions.Section)).ValidateOnStart();
            services.TryAddSingleton<IValidateOptions<SmtpEmailOptions>, SmtpEmailOptionsValidator>();
            services.TryAddSingleton<IEmailSender, SmtpEmailSender>();
            return services;
        }

        services.TryAddSingleton<FakeEmailSender>();
        services.TryAddSingleton<IEmailSender>(sp =>
        {
            var environment = sp.GetRequiredService<IHostEnvironment>();
            if (!environment.IsDevelopment() && !string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Email:Smtp:Host is required outside Development and Testing.");
            }

            return sp.GetRequiredService<FakeEmailSender>();
        });
        return services;
    }
}
