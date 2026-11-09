using Romp.BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Production.Application.Samples;

namespace Romp.Modules.Production.Infrastructure.Samples;

public sealed record SubmitPpSampleRequest(DateOnly? SubmittedDate, string ReportedBy, string ActingPerson, string? Note);

public sealed record DecidePpSampleRequest(bool Approve, string? Reason, string ActingPerson);

internal static class PpSampleEndpoints
{
    public static void MapPpSampleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/production/runs", Permissions.ProductionRead).WithTags("Production");

        group.MapGet("/{id:long}/pp-rounds", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetPpSampleRoundsQuery(id), cancellationToken)));

        group.MapPost("/{id:long}/pp-rounds", async (long id, SubmitPpSampleRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SubmitPpSampleCommand(id, body.SubmittedDate, body.ReportedBy, body.ActingPerson, body.Note), cancellationToken))).RequirePermission(Permissions.ProductionWrite);

        group.MapPost("/{id:long}/pp-rounds/{no:int}/decision", async (long id, int no, DecidePpSampleRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new DecidePpSampleCommand(id, no, body.Approve, body.Reason, body.ActingPerson), cancellationToken))).RequirePermission(Permissions.ProductionWrite);

        // Multipart upload; the content type is detected from the bytes, never trusted from the client (D7, AC-12).
        group.MapPost("/{id:long}/pp-rounds/{no:int}/files", async (
                long id, int no, IFormFile file, ISender sender, CancellationToken cancellationToken) =>
            {
                await using var stream = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken);
                return Results.Ok(await sender.Send(new UploadPpSampleFileCommand(id, no, file.FileName, buffer.ToArray()), cancellationToken));
            }).RequirePermission(Permissions.ProductionWrite)
            .DisableAntiforgery();

        group.MapDelete("/{id:long}/pp-rounds/{no:int}/files/{fileId:long}", async (long id, int no, long fileId, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new RemovePpSampleFileCommand(id, no, fileId), cancellationToken);
            return Results.NoContent();
        }).RequirePermission(Permissions.ProductionWrite);

        group.MapGet("/{id:long}/pp-rounds/{no:int}/files/{fileId:long}", async (
            long id, int no, long fileId, HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            var download = await sender.Send(new GetPpSampleFileQuery(id, no, fileId), cancellationToken);
            foreach (var (name, value) in download.Headers)
            {
                http.Response.Headers[name] = value;
            }

            return Results.File(download.Content, download.ContentType);
        });
    }
}
