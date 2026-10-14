using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Warehouse.Domain.Adjustments;

/// <summary>WHSE.ADJ_RSN_LKP: the four reason codes of BRD §5.14. More can be added as lookup rows without a deployment (D12).</summary>
public static class AdjustmentReasons
{
    public const short Damage = 1;
    public const short Miscount = 2;
    public const short Theft = 3;
    public const short FoundStock = 4;

    public static bool IsKnown(short id) => id is Damage or Miscount or Theft or FoundStock;

    public static string CodeOf(short id) => id switch
    {
        Damage => "DAMAGE",
        Miscount => "MISCOUNT",
        Theft => "THEFT",
        FoundStock => "FOUND_STOCK",
        _ => throw new DomainException($"Unknown adjustment reason {id}."),
    };
}

/// <summary>WHSE.ADJ_RSN_LKP</summary>
public sealed class AdjustmentReasonLookup : WarehouseLookup;

public static class AdjustmentMessages
{
    /// <summary>The client shows its confirmation step when a refusal starts with this text (ADJ-4b).</summary>
    public const string ReconfirmPrefix = "Confirmation required";
}

/// <summary>
/// The rules of an adjustment, in one pure place (ADJ-1, ADJ-3, ADJ-4b, ADJ-7): a known reason, a non-zero whole change within bounds,
/// a direction that fits the reason, and an explicit re-confirmation above the large-adjustment limit. The re-confirmation guards
/// against accidents (a mistyped 200), not against fraud: the server cannot tell who clicked (decision D5).
/// </summary>
public static class AdjustmentPolicy
{
    public const int MaxChange = 100_000;

    public static void Check(short reasonId, int change, int largeLimit, bool reconfirmed)
    {
        if (!AdjustmentReasons.IsKnown(reasonId))
        {
            throw new DomainException("Choose a reason: damage, miscount, theft or found stock.");
        }

        if (change == 0 || Math.Abs((long)change) > MaxChange)
        {
            throw new DomainException($"The change must be a whole number from 1 to {MaxChange}, up or down.");
        }

        if (reasonId == AdjustmentReasons.FoundStock && change < 0)
        {
            throw new DomainException("Found stock adds units. Use damage, theft or miscount to remove them.");
        }

        if (reasonId is AdjustmentReasons.Damage or AdjustmentReasons.Theft && change > 0)
        {
            throw new DomainException($"{AdjustmentReasons.CodeOf(reasonId)} removes units. Use found stock or miscount to add them.");
        }

        if (Math.Abs((long)change) > largeLimit && !reconfirmed)
        {
            throw new DomainException(
                $"{AdjustmentMessages.ReconfirmPrefix}: changing {Math.Abs(change)} units is above {largeLimit}. Confirm it to continue.");
        }
    }
}

/// <summary>
/// One correction of stock (WHSE.ADJ_MAIN): immutable, attributable, always with a reason (ADJ-6, NFR-SC-05). A mistake is fixed by a
/// new, opposite adjustment, never by editing this one. Cycle counts create these too, linked to the count line.
/// </summary>
public sealed class StockAdjustment : Entity<long>, IAuditable
{
    private StockAdjustment()
    {
        ActorName = string.Empty;
    }

    public StockAdjustment(
        long variantRefId, short gradeId, long binId, int quantityChange, short reasonId, string? note, bool reconfirmed,
        Guid idempotencyKey, long? countLineId, string actorName)
    {
        if (note is not null && (note.Length > 200 || note.Any(char.IsControl)))
        {
            throw new DomainException("A note can be at most 200 characters, without control characters.");
        }

        VariantRefId = variantRefId;
        GradeId = gradeId;
        BinId = binId;
        QuantityChange = quantityChange;
        ReasonId = reasonId;
        Note = note;
        Reconfirmed = reconfirmed;
        IdempotencyKey = idempotencyKey;
        CountLineId = countLineId;
        ActorName = actorName;
    }

    public long VariantRefId { get; private set; }

    public short GradeId { get; private set; }

    public long BinId { get; private set; }

    public int QuantityChange { get; private set; }

    public short ReasonId { get; private set; }

    public string? Note { get; private set; }

    public bool Reconfirmed { get; private set; }

    public Guid IdempotencyKey { get; private set; }

    public long? CountLineId { get; private set; }

    public string ActorName { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
