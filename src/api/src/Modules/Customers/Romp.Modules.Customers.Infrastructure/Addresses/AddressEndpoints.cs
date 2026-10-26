using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Customers.Application.Addresses;

namespace Romp.Modules.Customers.Infrastructure.Addresses;

/// <summary>The body of an add or edit: what the customer typed, and nothing about who they are.</summary>
public sealed record AddressRequest(string RecipientName, string Phone, short ProvinceId, short CityId, string LineOne, string? LineTwo, string? Postcode);

/// <summary>
/// The caller's own address book. No route or body carries a customer id: every handler takes the customer from the signed-in principal and
/// filters by it in the same statement, so another customer's address id is a plain 404 (ADD-3, SEC-05).
/// </summary>
internal static class AddressEndpoints
{
    private const string Prefix = "/api/customers/me/addresses";

    public static void MapAddressEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var book = endpoints.MapSecuredGroup("/api/customers/me/addresses", Policies.Customer).WithTags("Customers").AddEndpointFilter<CustomerNoStoreFilter>();

        book.MapGet("", async (ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListAddressesQuery(page, pageSize), cancellationToken)));

        book.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetAddressQuery(id), cancellationToken)));

        book.MapPost("", async (AddressRequest body, ISender sender, CancellationToken cancellationToken) =>
        {
            var created = await sender.Send(
                new AddAddressCommand(body.RecipientName, body.Phone, body.ProvinceId, body.CityId, body.LineOne, body.LineTwo, body.Postcode), cancellationToken);
            return Results.Created($"{Prefix}/{created.Id}", created);
        });

        book.MapPut("/{id:long}", async (long id, AddressRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new UpdateAddressCommand(id, body.RecipientName, body.Phone, body.ProvinceId, body.CityId, body.LineOne, body.LineTwo, body.Postcode), cancellationToken)));

        book.MapPost("/{id:long}/default", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new MakeDefaultAddressCommand(id), cancellationToken)));

        book.MapDelete("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new RemoveAddressCommand(id), cancellationToken);
            return Results.NoContent();
        });
    }
}
