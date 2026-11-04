using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Cart.Domain.DiscountCodes;

/// <summary>
/// One use of a code by one order, in three steps (ADR 0025): <c>Held</c> while the order is being placed, <c>Confirmed</c> once the order
/// exists, <c>Released</c> if it never did or was cancelled. The placement key is unique, so a repeated hold is the same hold (DSC-10).
/// </summary>
public sealed class Redemption : Entity<long>, IAuditable
{
    private Redemption()
    {
        PlacementKey = string.Empty;
        CustomerKey = string.Empty;
        InsrBy = string.Empty;
    }

    public Redemption(long discountCodeId, string placementKey, string customerKey, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(placementKey) || string.IsNullOrWhiteSpace(customerKey))
        {
            throw new DomainException("A redemption needs a placement key and a customer key.");
        }

        DiscountCodeId = discountCodeId;
        PlacementKey = placementKey;
        CustomerKey = customerKey;
        Amount = amount;
        Status = RedemptionStatus.Held;
        InsrBy = string.Empty;
    }

    public long DiscountCodeId { get; private set; }

    public string PlacementKey { get; private set; }

    /// <summary>The signed-in customer ("C" and the id) or, for a guest, the hash of their normalised mobile ("M" and the hash).</summary>
    public string CustomerKey { get; private set; }

    public decimal Amount { get; private set; }

    public RedemptionStatus Status { get; private set; }

    public string? OrderNumber { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>The order exists. Repeating it, or confirming one already confirmed, changes nothing.</summary>
    public void Confirm(string orderNumber)
    {
        if (Status == RedemptionStatus.Held)
        {
            Status = RedemptionStatus.Confirmed;
            OrderNumber = orderNumber;
        }
    }

    /// <summary>The order never came, or was cancelled. Releasing twice changes nothing.</summary>
    public void Release()
    {
        if (Status != RedemptionStatus.Released)
        {
            Status = RedemptionStatus.Released;
        }
    }

    public bool CountsAsUse => Status is RedemptionStatus.Held or RedemptionStatus.Confirmed;
}
