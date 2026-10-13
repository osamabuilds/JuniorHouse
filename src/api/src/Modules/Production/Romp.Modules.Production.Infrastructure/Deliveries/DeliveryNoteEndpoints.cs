using Romp.BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Production.Application.Deliveries;
using Romp.Modules.Production.Application.ProductionRuns;

namespace Romp.Modules.Production.Infrastructure.Deliveries;

public sealed record RecordDeliveryNoteRequest(
    string VendorDnNo,
    DateOnly DispatchDate,
    bool IsFinalShipment,
    IReadOnlyList<QuantityLineDto> Lines,
    string ActingPerson);

internal static class DeliveryNoteEndpoints
{
    public static void MapDeliveryNoteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/production/runs").WithTags("Production");

        group.MapGet("/{id:long}/delivery-notes", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetDeliveryNotesQuery(id), cancellationToken)));

        group.MapPost("/{id:long}/delivery-notes", async (long id, RecordDeliveryNoteRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new RecordDeliveryNoteCommand(id, body.VendorDnNo, body.DispatchDate, body.IsFinalShipment, body.Lines, body.ActingPerson),
                cancellationToken)));
    }
}
