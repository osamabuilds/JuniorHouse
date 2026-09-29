using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Domain;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Application.Vendors;

/// <summary>FR-SC-01, AC-5, AC-5a: creates a vendor with one or more specialisations.</summary>
public sealed record CreateVendorCommand(
    string Name,
    string ContactName,
    string ContactPhone,
    string? ContactEmail,
    short CityId,
    short PaymentTermId,
    IReadOnlyCollection<short> SpecialisationIds) : IRequest<VendorDto>, IVendorCommand;

public sealed class CreateVendorCommandValidator : AbstractValidator<CreateVendorCommand>
{
    public CreateVendorCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.ContactName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.ContactPhone).NotEmpty().MaximumLength(20);
        RuleFor(c => c.ContactEmail).MaximumLength(200).EmailAddress().When(c => !string.IsNullOrEmpty(c.ContactEmail));
        RuleFor(c => c.SpecialisationIds).NotEmpty().WithMessage("At least one specialisation is required.");
    }
}

public sealed class CreateVendorCommandHandler(IVendorDbContext dbContext) : IRequestHandler<CreateVendorCommand, VendorDto>
{
    public async Task<VendorDto> Handle(CreateVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = new Domain.Vendors.Vendor(
            request.Name,
            request.ContactName,
            request.ContactPhone,
            request.ContactEmail,
            request.CityId,
            request.PaymentTermId);

        dbContext.Vendors.Add(vendor);

        // Saved here so Vendor.Id is generated before SetSpecialisations fixes up VNDR_SPCL_MAP's
        // VendorId, and again so the response DTO carries the real id.
        await dbContext.SaveChangesAsync(cancellationToken);

        vendor.SetSpecialisations(request.SpecialisationIds);
        await dbContext.SaveChangesAsync(cancellationToken);

        return vendor.ToDto();
    }
}
