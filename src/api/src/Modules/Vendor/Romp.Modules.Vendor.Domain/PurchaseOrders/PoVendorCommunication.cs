using Romp.BuildingBlocks.Domain;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Domain.PurchaseOrders;

/// <summary>Maps to VNDR.PO_VNDR_COMM - a captured vendor response/counter/decision, optionally tied to a specific revision. Task 19's schema-only skeleton; task 30+ adds capture behaviour.</summary>
public sealed class PoVendorCommunication : Entity<long>, IAuditable
{
    private PoVendorCommunication()
    {
        ResponderName = string.Empty;
    }

    /// <summary>Created only via <see cref="PurchaseOrder.RecordVendorCommunication"/> (SCRUM-93 task 30) - <paramref name="revisionId"/> must already be a real (saved) id, since this entity is added directly to the aggregate's own collection, not fixed up through a navigation.</summary>
    internal PoVendorCommunication(long poId, long? revisionId, short communicationTypeId, short channelId, string responderName, DateTimeOffset responseDte)
    {
        PoId = poId;
        RevisionId = revisionId;
        CommunicationTypeId = communicationTypeId;
        ChannelId = channelId;
        ResponderName = responderName;
        ResponseDte = responseDte;
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
