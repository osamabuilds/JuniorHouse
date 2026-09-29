using Romp.BuildingBlocks.Domain;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Domain.Files;

/// <summary>
/// Maps to VNDR.PO_FILE - a file attached to a PO, either vendor-visible (versioned via its
/// <see cref="AddedInRevisionId"/>/<see cref="RetiredInRevisionId"/> effective window) or internal
/// (never versioned). Task 19's schema-only skeleton; task 34+ adds upload/retire behaviour.
/// </summary>
public sealed class PurchaseOrderFile : Entity<long>, IAuditable
{
    private PurchaseOrderFile()
    {
        FileName = string.Empty;
        StorageKey = string.Empty;
        ContentType = string.Empty;
    }

    public PurchaseOrderFile(
        long poId, short categoryId, string fileName, string storageKey, string contentType, long fileSizeBytes, long? addedInRevisionId,
        long? vendorCommunicationId = null)
    {
        PoId = poId;
        CategoryId = categoryId;
        FileName = fileName;
        StorageKey = storageKey;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        AddedInRevisionId = addedInRevisionId;
        VendorCommunicationId = vendorCommunicationId;
    }

    /// <summary>Draft-stage removal only (<see cref="PoFilePolicy"/> decides when); the bytes stay in storage.</summary>
    public void SoftDelete() => IsDeleted = true;

    /// <summary>A vendor-visible file taken out of effect by an amendment's revision.</summary>
    public void RetireIn(long revisionId) => RetiredInRevisionId = revisionId;

    public void SetAddedInRevision(long revisionId) => AddedInRevisionId = revisionId;

    public long PoId { get; private set; }

    public short CategoryId { get; private set; }

    public string FileName { get; private set; }

    /// <summary>Generated, never derived from <see cref="FileName"/> (SEC: predictable-storage-key risk).</summary>
    public string StorageKey { get; private set; }

    public string ContentType { get; private set; }

    public long FileSizeBytes { get; private set; }

    /// <summary>Null = internal file, or a Draft-stage vendor-visible file not yet sent.</summary>
    public long? AddedInRevisionId { get; private set; }

    public long? RetiredInRevisionId { get; private set; }

    /// <summary>Draft-stage soft delete.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Set only when <see cref="CategoryId"/> is VendorEvidence - the vendor communication this evidence supports.</summary>
    public long? VendorCommunicationId { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
