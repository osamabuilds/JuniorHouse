using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Warehouse.Domain.StockAlerts;

/// <summary>WHSE.ALRT_LVL_LKP</summary>
public static class AlertLevels
{
    public const short Low = 1;
    public const short Out = 2;

    public static bool IsKnown(short id) => id is Low or Out;

    public static string CodeOf(short id) => id switch
    {
        Low => "LOW",
        Out => "OUT",
        _ => throw new DomainException($"Unknown alert level {id}."),
    };
}

/// <summary>WHSE.ALRT_STS_LKP</summary>
public static class AlertStatuses
{
    public const short Open = 1;
    public const short Closed = 2;
}

/// <summary>WHSE.ALRT_LVL_LKP</summary>
public sealed class AlertLevelLookup : WarehouseLookup;

/// <summary>WHSE.ALRT_STS_LKP</summary>
public sealed class AlertStatusLookup : WarehouseLookup;

public enum AlertAction
{
    None,
    Open,
    ChangeLevel,
    Close,
}

public readonly record struct AlertDecision(AlertAction Action, short Level);

/// <summary>
/// What an alert should do after a stock change (ALR-1..4), as a pure function of the numbers. One alert per variant crossing the
/// threshold, never one per movement; stock-out only for a variant that has actually held stock; restocking above the threshold closes it.
/// </summary>
public static class AlertPolicy
{
    public static AlertDecision Evaluate(int available, int threshold, bool hasEverHeldStock, short? currentOpenLevel)
    {
        if (available > threshold)
        {
            return currentOpenLevel is null ? new(AlertAction.None, 0) : new(AlertAction.Close, 0);
        }

        if (available <= 0)
        {
            if (!hasEverHeldStock)
            {
                return currentOpenLevel is null ? new(AlertAction.None, 0) : new(AlertAction.Close, 0); // ALR-4
            }

            return currentOpenLevel switch
            {
                null => new(AlertAction.Open, AlertLevels.Out),
                AlertLevels.Out => new(AlertAction.None, 0),
                _ => new(AlertAction.ChangeLevel, AlertLevels.Out), // ALR-2: Low becomes Stock-out, not a second alert
            };
        }

        return currentOpenLevel switch
        {
            null => new(AlertAction.Open, AlertLevels.Low),
            AlertLevels.Low => new(AlertAction.None, 0),
            _ => new(AlertAction.ChangeLevel, AlertLevels.Low), // stock came back from zero but is still low
        };
    }
}

/// <summary>A per-variant override of the low-stock threshold (D13: per-variant only this sprint). Absent means the configured default applies.</summary>
public sealed class LowStockThreshold : Entity<long>, IAuditable
{
    public const int Max = 100_000;

    private LowStockThreshold()
    {
    }

    public LowStockThreshold(long variantRefId, int quantity)
    {
        VariantRefId = variantRefId;
        Set(quantity);
    }

    public long VariantRefId { get; private set; }

    public int Quantity { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public void Set(int quantity)
    {
        if (quantity is < 0 or > Max)
        {
            throw new DomainException($"A low-stock threshold is a whole number from 0 to {Max}.");
        }

        Quantity = quantity;
    }
}

/// <summary>
/// A low-stock or stock-out alert for one variant (FR-54, WHSE.ALRT_MAIN). At most one is open per variant (a partial unique index backs
/// this). It records who acknowledged it and closes by itself on restock. Opening or changing level raises
/// <see cref="StockAlertRaisedEvent"/> for later notification wiring (D6).
/// </summary>
public sealed class StockAlert : AggregateRoot<long>, IAuditable
{
    private StockAlert()
    {
    }

    private StockAlert(long variantRefId, short levelId, int lastQuantity, int threshold, DateTimeOffset at)
    {
        VariantRefId = variantRefId;
        LevelId = levelId;
        StatusId = AlertStatuses.Open;
        LastQuantity = lastQuantity;
        Threshold = threshold;
        OpenedAt = at;
    }

    public long VariantRefId { get; private set; }

    public short LevelId { get; private set; }

    public short StatusId { get; private set; }

    public int LastQuantity { get; private set; }

    public int Threshold { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public string? AcknowledgedBy { get; private set; }

    public bool IsOpen => StatusId == AlertStatuses.Open;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Opens an alert and raises the event. The caller supplies the Labelling variant id and SKU the event carries.</summary>
    public static StockAlert Open(long variantRefId, long variantId, string skuCode, short levelId, int available, int threshold, DateTimeOffset at)
    {
        var alert = new StockAlert(variantRefId, levelId, available, threshold, at);
        alert.Raise(new StockAlertRaisedEvent(variantId, skuCode, AlertLevels.CodeOf(levelId), available, threshold));
        return alert;
    }

    /// <summary>The open alert moves between Low and Stock-out (ALR-2); acknowledgement is cleared because the situation changed.</summary>
    public void ChangeLevel(long variantId, string skuCode, short levelId, int available, int threshold)
    {
        EnsureOpen();
        LevelId = levelId;
        LastQuantity = available;
        Threshold = threshold;
        AcknowledgedAt = null;
        AcknowledgedBy = null;
        Raise(new StockAlertRaisedEvent(variantId, skuCode, AlertLevels.CodeOf(levelId), available, threshold));
    }

    /// <summary>Keeps the numbers shown on the alert current without raising anything.</summary>
    public void Observe(int available, int threshold)
    {
        LastQuantity = available;
        Threshold = threshold;
    }

    public void Close(DateTimeOffset at, int available, int threshold)
    {
        EnsureOpen();
        StatusId = AlertStatuses.Closed;
        ClosedAt = at;
        LastQuantity = available;
        Threshold = threshold;
    }

    /// <summary>Records who has seen it (ALR-5). The alert stays open until stock recovers.</summary>
    public void Acknowledge(string person, DateTimeOffset at)
    {
        EnsureOpen();
        AcknowledgedBy = person;
        AcknowledgedAt = at;
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new DomainException("This alert is already closed.");
        }
    }
}
