using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Catalog.Application.CompositionCare;

namespace Romp.Modules.Catalog.Infrastructure.CompositionCare;

/// <summary>Body of PUT /api/catalog/products/{id}/composition.</summary>
public sealed record SetCompositionRequest(IReadOnlyList<CompositionRowInput> Rows, uint Version, string ActingPerson);

/// <summary>Body of PUT /api/catalog/products/{id}/care.</summary>
public sealed record SetCareRequest(IReadOnlyList<short> CareIds, string? Note, uint Version, string ActingPerson);

// Mapped through MapSecuredGroup with the Catalogue policy (ADR 0011, SEC-08).
internal static class CompositionCareEndpoints
{
    private const long MaxBodyBytes = 16 * 1024;

    public static void MapCompositionCareEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/catalog/products", Policies.Catalogue).WithTags("Catalog")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        group.MapGet("/{id:long}/composition-care", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetCompositionAndCareQuery(id), cancellationToken)));

        group.MapPut("/{id:long}/composition", async (long id, SetCompositionRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SetCompositionCommand(id, body.Rows, body.Version, body.ActingPerson), cancellationToken)));

        group.MapPut("/{id:long}/care", async (long id, SetCareRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SetCareCommand(id, body.CareIds, body.Note, body.Version, body.ActingPerson), cancellationToken)));
    }
}
