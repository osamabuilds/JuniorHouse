using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Romp.BuildingBlocks.Modules;

/// <summary>
/// Entry point each domain module exposes to the host. Modules never reference each other
/// directly; they communicate through integration events (enforced by architecture tests).
/// </summary>
public interface IModule
{
    string Name { get; }

    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
