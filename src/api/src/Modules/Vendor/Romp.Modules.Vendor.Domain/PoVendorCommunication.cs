using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Vendor.Domain;

/// <summary>Maps to VNDR.PO_VNDR_COMM - a captured vendor response/counter/decision, optionally tied to a specific revision. Task 19's schema-only skeleton; task 30+ adds capture behaviour.</summary>
public sealed class PoVendorCommunication : Entity<long>, IAuditable
{
    private PoVendorCommunication()
    {
        ResponderName = string.Empty;
    }

    public long PoId { get; private set; }

    public long? RevisionId { get; private set; }

    public short CommunicationTypeId { get; private set; }

    public short ChannelId { get; private set; }

    public string ResponderName { get; private set; }

    public DateTimeOffset ResponseDte { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
