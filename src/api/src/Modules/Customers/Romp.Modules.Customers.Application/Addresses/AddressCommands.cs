using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Addresses;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Customers.Application.Addresses;

/// <summary>
/// One saved address as the account screen shows it, with the province and city names already joined in. The customer is never part of it:
/// the caller is the owner (SEC-05).
/// </summary>
public sealed record AddressDto(
    long Id, string RecipientName, string Phone, short ProvinceId, string ProvinceName, short CityId, string CityName,
    string LineOne, string? LineTwo, string? Postcode, bool IsDefault);

/// <summary>The fields a customer types for an address; add and update share them and their validation.</summary>
public interface IAddressInput
{
    string RecipientName { get; }

    string Phone { get; }

    short ProvinceId { get; }

    short CityId { get; }

    string LineOne { get; }

    string? LineTwo { get; }

    string? Postcode { get; }
}

public sealed record AddAddressCommand(string RecipientName, string Phone, short ProvinceId, short CityId, string LineOne, string? LineTwo, string? Postcode)
    : IRequest<AddressDto>, ICustomerCommand, IAddressInput;

public sealed record UpdateAddressCommand(long Id, string RecipientName, string Phone, short ProvinceId, short CityId, string LineOne, string? LineTwo, string? Postcode)
    : IRequest<AddressDto>, ICustomerCommand, IAddressInput;

public sealed record RemoveAddressCommand(long Id) : IRequest, ICustomerCommand;

public sealed record MakeDefaultAddressCommand(long Id) : IRequest<AddressDto>, ICustomerCommand;

/// <summary>Add, make-default and remove lock the customer's row first, so the limit and the single default hold under concurrent requests (ADD-2, ADD-6).</summary>
public sealed class AddAddressCommandHandler(ICustomerDbContext dbContext, ICurrentCustomer current, IGeographyQueries geography, IOptions<CustomersOptions> options)
    : IRequestHandler<AddAddressCommand, AddressDto>
{
    public async Task<AddressDto> Handle(AddAddressCommand request, CancellationToken cancellationToken)
    {
        var customerId = current.Require();
        await dbContext.LockCustomerAsync(customerId, cancellationToken);
        var book = await dbContext.Addresses.Where(a => a.CustomerId == customerId).ToListAsync(cancellationToken);

        var address = CustomerAddress.Create(customerId, request.RecipientName, request.Phone, request.ProvinceId, request.CityId, request.LineOne, request.LineTwo, request.Postcode);
        AddressBook.Add(book, address, options.Value.MaxAddresses);
        dbContext.Addresses.Add(address);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (await AddressMapper.ToDtosAsync([address], geography, cancellationToken))[0];
    }
}

public sealed class UpdateAddressCommandHandler(ICustomerDbContext dbContext, ICurrentCustomer current, IGeographyQueries geography)
    : IRequestHandler<UpdateAddressCommand, AddressDto>
{
    public async Task<AddressDto> Handle(UpdateAddressCommand request, CancellationToken cancellationToken)
    {
        var customerId = current.Require();
        var address = await dbContext.Addresses.FirstOrDefaultAsync(a => a.Id == request.Id && a.CustomerId == customerId, cancellationToken)
            ?? throw new KeyNotFoundException("Address not found.");

        address.Update(request.RecipientName, request.Phone, request.ProvinceId, request.CityId, request.LineOne, request.LineTwo, request.Postcode);

        return (await AddressMapper.ToDtosAsync([address], geography, cancellationToken))[0];
    }
}

public sealed class MakeDefaultAddressCommandHandler(ICustomerDbContext dbContext, ICurrentCustomer current, IGeographyQueries geography)
    : IRequestHandler<MakeDefaultAddressCommand, AddressDto>
{
    public async Task<AddressDto> Handle(MakeDefaultAddressCommand request, CancellationToken cancellationToken)
    {
        var customerId = current.Require();
        await dbContext.LockCustomerAsync(customerId, cancellationToken);
        var book = await dbContext.Addresses.Where(a => a.CustomerId == customerId).ToListAsync(cancellationToken);
        var address = book.FirstOrDefault(a => a.Id == request.Id) ?? throw new KeyNotFoundException("Address not found.");

        if (!address.IsDefault)
        {
            // Clear the old default and save before setting the new one: the unique index allows no moment with two defaults.
            AddressBook.ClearDefault(book);
            await dbContext.SaveChangesAsync(cancellationToken);
            AddressBook.MakeDefault(book, address);
        }

        return (await AddressMapper.ToDtosAsync([address], geography, cancellationToken))[0];
    }
}

public sealed class RemoveAddressCommandHandler(ICustomerDbContext dbContext, ICurrentCustomer current) : IRequestHandler<RemoveAddressCommand>
{
    public async Task Handle(RemoveAddressCommand request, CancellationToken cancellationToken)
    {
        var customerId = current.Require();
        await dbContext.LockCustomerAsync(customerId, cancellationToken);
        var book = await dbContext.Addresses.Where(a => a.CustomerId == customerId).ToListAsync(cancellationToken);
        var address = book.FirstOrDefault(a => a.Id == request.Id) ?? throw new KeyNotFoundException("Address not found.");

        var wasDefault = address.IsDefault;
        dbContext.Addresses.Remove(address);
        await dbContext.SaveChangesAsync(cancellationToken);

        // The deleted row must be gone before another becomes the default (the unique index), so the promotion is a second statement.
        if (wasDefault)
        {
            AddressBook.Remove(book, address);
        }
    }
}
