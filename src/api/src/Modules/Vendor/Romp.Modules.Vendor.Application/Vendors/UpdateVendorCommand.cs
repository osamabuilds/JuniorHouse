using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Application.Vendors;

/// <summary>AC-6: edits contact, city, specialisations, default payment term, or active flag.</summary>
public sealed record UpdateVendorCommand(
    long Id,
    string ContactName,
    string ContactPhone,
    string? ContactEmail,
    short CityId,
    short PaymentTermId,
    IReadOnlyCollection<short> SpecialisationIds,
    bool IsActive) : IRequest<VendorDto>, IVendorCommand;

public sealed class UpdateVendorCommandValidator : AbstractValidator<UpdateVendorCommand>
{
    public UpdateVendorCommandValidator()
    {
        RuleFor(c => c.ContactName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.ContactPhone).NotEmpty().MaximumLength(20);
        RuleFor(c => c.ContactEmail).MaximumLength(200).EmailAddress().When(c => !string.IsNullOrEmpty(c.ContactEmail));
        RuleFor(c => c.SpecialisationIds).NotEmpty().WithMessage("At least one specialisation is required.");
    }
}

public sealed class UpdateVendorCommandHandler(IVendorDbContext dbContext) : IRequestHandler<UpdateVendorCommand, VendorDto>
{
    public async Task<VendorDto> Handle(UpdateVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await dbContext.Vendors
            .Include(v => v.Specialisations)
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Vendor {request.Id} was not found.");

        vendor.UpdateDetails(
            request.ContactName,
            request.ContactPhone,
            request.ContactEmail,
            request.CityId,
            request.PaymentTermId,
            request.IsActive);

        vendor.SetSpecialisations(request.SpecialisationIds);

        // Saved here (not left to VendorTransactionBehavior) so the response DTO reflects
        // UPDT_DTE/BY as stamped by AuditSaveChangesInterceptor, not the pre-save in-memory value.
        await dbContext.SaveChangesAsync(cancellationToken);

        return vendor.ToDto();
    }
}
