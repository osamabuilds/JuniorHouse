using FluentValidation;
using Romp.Modules.Vendor.Domain;

namespace Romp.Modules.Vendor.Application;

/// <summary>SCRUM-93 (AC-31, AC-34): optional evidence for something the vendor said - e.g. a WhatsApp screenshot. Stored as an internal file, never shown to the vendor.</summary>
public sealed record EvidenceFileAdd(string FileName, byte[] Content);

/// <summary>Same content rules as any PO file (type by signature, size limit) - with messages that say it's the evidence file.</summary>
public sealed class EvidenceFileAddValidator : AbstractValidator<EvidenceFileAdd>
{
    public EvidenceFileAddValidator(PoFileStorageOptions options)
    {
        RuleFor(f => f.FileName).NotEmpty().WithMessage("Each evidence file needs a name.");
        RuleFor(f => f.Content)
            .Must(content => content.Length > 0).WithMessage("An evidence file is empty.")
            .Must(content => content.LongLength <= options.MaxFileSizeBytes)
            .WithMessage($"An evidence file is larger than the {options.MaxFileSizeBytes / (1024 * 1024)} MB limit.")
            .Must(content => content.Length == 0 || PoFileContent.DetectContentType(content) is not null)
            .WithMessage($"An evidence file has a type that isn't allowed. Use {PoFileContent.AllowedTypesDescription}.");
    }
}

internal static class VendorEvidence
{
    /// <summary>
    /// Stores each evidence file as an internal <see cref="PoFileCategory.VendorEvidence"/> file linked
    /// to <paramref name="communication"/>, which must already be saved (it needs its real id).
    /// The caller saves afterwards.
    /// </summary>
    public static async Task AddAsync(
        IVendorDbContext dbContext,
        IFileStorage fileStorage,
        long poId,
        PoVendorCommunication communication,
        IReadOnlyCollection<EvidenceFileAdd>? evidence,
        CancellationToken cancellationToken)
    {
        foreach (var item in evidence ?? [])
        {
            var storageKey = await fileStorage.SaveAsync(item.Content, cancellationToken);
            dbContext.PurchaseOrderFiles.Add(new PurchaseOrderFile(
                poId, PoFileCategory.VendorEvidence, PoFileContent.SanitiseFileName(item.FileName), storageKey,
                PoFileContent.DetectContentType(item.Content)!, item.Content.LongLength, addedInRevisionId: null,
                vendorCommunicationId: communication.Id));
        }
    }
}
