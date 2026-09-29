using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Domain;
using Romp.Modules.Vendor.Domain.Files;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Application.Files;

/// <summary>SCRUM-93 task 35 (AC-37, AC-40, AC-41, AC-45, AC-42). Adds a file to a PO under <see cref="PoFilePolicy"/>'s status rules.</summary>
public sealed record UploadPoFileCommand(long PoId, short CategoryId, string FileName, byte[] Content) : IRequest<PoFileDto>, IVendorCommand;

public sealed class UploadPoFileCommandValidator : AbstractValidator<UploadPoFileCommand>
{
    public UploadPoFileCommandValidator(PoFileStorageOptions options)
    {
        RuleFor(c => c.CategoryId)
            .Must(PoFileCategory.IsKnown).WithMessage("Choose a valid file category.");
        RuleFor(c => c.FileName).NotEmpty().WithMessage("The file needs a name.");
        RuleFor(c => c.Content)
            .Must(content => content.Length > 0).WithMessage("The file is empty.")
            .Must(content => content.LongLength <= options.MaxFileSizeBytes)
            .WithMessage($"The file is larger than the {options.MaxFileSizeBytes / (1024 * 1024)} MB limit.")
            .Must(content => content.Length == 0 || PoFileContent.DetectContentType(content) is not null)
            .WithMessage($"This file type isn't allowed. Upload a {PoFileContent.AllowedTypesDescription} file.");
    }
}

public sealed class UploadPoFileCommandHandler(IVendorDbContext dbContext, IFileStorage fileStorage, PoFileStorageOptions options)
    : IRequestHandler<UploadPoFileCommand, PoFileDto>
{
    public async Task<PoFileDto> Handle(UploadPoFileCommand request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == request.PoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PoId} was not found.");

        PoFilePolicy.EnsureCanAdd(po.PoNo, po.StatusId, request.CategoryId);

        var fileCount = await dbContext.PurchaseOrderFiles.CountAsync(f => f.PoId == po.Id && !f.IsDeleted, cancellationToken);
        if (fileCount >= options.MaxFilesPerPo)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Content)] = [$"PO {po.PoNo} already has the maximum of {options.MaxFilesPerPo} files."],
            });
        }

        // Validation has already passed, so the type is known. Bytes are stored only after every check.
        var contentType = PoFileContent.DetectContentType(request.Content)!;
        var storageKey = await fileStorage.SaveAsync(request.Content, cancellationToken);

        var file = new PurchaseOrderFile(
            po.Id, request.CategoryId, PoFileContent.SanitiseFileName(request.FileName), storageKey, contentType, request.Content.LongLength, addedInRevisionId: null);
        dbContext.PurchaseOrderFiles.Add(file);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PoFileDto(file.Id, file.FileName, file.CategoryId, PoFileCategory.IsVendorVisible(file.CategoryId),
            file.FileSizeBytes, file.InsrBy, file.InsrDte, null, null);
    }
}
