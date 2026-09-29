using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Vendor.Application.Files;

namespace Romp.Modules.Vendor.Infrastructure.Files;

internal static class PurchaseOrderFileEndpoints
{
    public static void MapPurchaseOrderFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/purchase-orders").WithTags("Vendor");

        // SCRUM-93 task 40. Multipart upload; the content type is detected from the bytes, never trusted from the client.
        group.MapGet("/{id:long}/files", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetPoFileListQuery(id), cancellationToken)));

        group.MapPost("/{id:long}/files", async (
                long id,
                [Microsoft.AspNetCore.Mvc.FromForm] short categoryId,
                IFormFile file,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await using var stream = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken);
                return Results.Ok(await sender.Send(new UploadPoFileCommand(id, categoryId, file.FileName, buffer.ToArray()), cancellationToken));
            })
            .DisableAntiforgery();

        group.MapDelete("/{id:long}/files/{fileId:long}", async (long id, long fileId, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new RemovePoFileCommand(id, fileId), cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/{id:long}/files/{fileId:long}", async (
            long id, long fileId, HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            var download = await sender.Send(new DownloadPoFileQuery(id, fileId), cancellationToken);
            foreach (var (name, value) in download.Headers)
            {
                http.Response.Headers[name] = value;
            }

            return Results.File(download.Content, download.ContentType);
        });
    }
}
