using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Romp.BuildingBlocks.Persistence.Auditing;

namespace Romp.BuildingBlocks.Web;

public static class CurrentActorExtensions
{
    /// <summary>Replaces the fixed <c>system</c> actor with one that reads the signed-in user.</summary>
    public static IServiceCollection AddHttpContextCurrentActor(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.Replace(ServiceDescriptor.Scoped<ICurrentActor, HttpContextCurrentActor>());
        return services;
    }
}
