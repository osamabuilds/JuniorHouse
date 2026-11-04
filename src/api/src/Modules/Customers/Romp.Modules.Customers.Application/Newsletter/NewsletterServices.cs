using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Customers.Application.Abstractions;

namespace Romp.Modules.Customers.Application.Newsletter;

public static class NewsletterServices
{
    /// <summary>Registers the newsletter's own services (consent rules and the registration opt-in). The mail sender and the audience live in Infrastructure.</summary>
    public static IServiceCollection AddCustomerNewsletter(this IServiceCollection services)
    {
        services.AddScoped<NewsletterConsent>();
        services.AddScoped<INewsletterOptIn, NewsletterOptIn>();
        return services;
    }
}
