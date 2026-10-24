using System.Security.Cryptography;
using System.Text;
using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Storefront.Domain.StorefrontSettings;

/// <summary>An age in the "Shop by age" list. The code is the stable key used in links; renaming the label never changes a URL (ST-2). Maps to STFR.AGE_STEP.</summary>
public sealed class AgeStep : Entity<long>, IAuditable
{
    private AgeStep() => (Code, Label) = (string.Empty, string.Empty);

    public AgeStep(string code, string label, int sortSeq, bool isEnabled = true)
    {
        Code = (code ?? string.Empty).Trim();
        Label = (label ?? string.Empty).Trim();
        SortSeq = sortSeq;
        IsEnabled = isEnabled;
    }

    public string Code { get; private set; }

    public string Label { get; private set; }

    public int SortSeq { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>A link in a footer column. Maps to STFR.FOOT_LINK.</summary>
public sealed class FooterLink : Entity<long>, IAuditable
{
    private FooterLink() => Label = string.Empty;

    public FooterLink(string label, LinkTarget target, int sortSeq, bool isEnabled = true)
    {
        Label = (label ?? string.Empty).Trim();
        TargetKind = target.Kind;
        TargetRef = target.Reference;
        SortSeq = sortSeq;
        IsEnabled = isEnabled;
    }

    public string Label { get; private set; }

    public LinkTargetKind TargetKind { get; private set; }

    public string TargetRef { get; private set; } = string.Empty;

    public LinkTarget Target => new(TargetKind, TargetRef);

    public int SortSeq { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>A titled footer column with ordered links. Maps to STFR.FOOT_COLM.</summary>
public sealed class FooterColumn : Entity<long>, IAuditable
{
    private readonly List<FooterLink> _links = [];

    private FooterColumn() => Title = string.Empty;

    public FooterColumn(string title, int sortSeq, IEnumerable<FooterLink>? links = null)
    {
        Title = (title ?? string.Empty).Trim();
        SortSeq = sortSeq;
        if (links is not null)
        {
            _links.AddRange(links);
        }
    }

    public string Title { get; private set; }

    public int SortSeq { get; private set; }

    public IReadOnlyList<FooterLink> Links => _links;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>A brand or contact value with a per-key rule (phone in E.164, email, length). Maps to STFR.STNG_ITEM.</summary>
public sealed class SettingItem : Entity<long>, IAuditable
{
    private SettingItem() => Value = string.Empty;

    public SettingItem(SettingKey key, string value)
    {
        Key = key;
        Value = (value ?? string.Empty).Trim();
    }

    public SettingKey Key { get; private set; }

    public string Value { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>Delivery time and charge for a city (PKR, <c>numeric(12,2)</c>). Maps to STFR.DLVR_RULE.</summary>
public sealed class DeliveryRule : Entity<long>, IAuditable
{
    private DeliveryRule() => (City, Courier) = (string.Empty, string.Empty);

    public DeliveryRule(string city, int minDays, int maxDays, decimal chargeAmt, decimal? freeThresholdAmt, string courier, bool isEnabled = true)
    {
        City = (city ?? string.Empty).Trim();
        MinDays = minDays;
        MaxDays = maxDays;
        ChargeAmt = chargeAmt;
        FreeThresholdAmt = freeThresholdAmt;
        Courier = (courier ?? string.Empty).Trim();
        IsEnabled = isEnabled;
    }

    public string City { get; private set; }

    public int MinDays { get; private set; }

    public int MaxDays { get; private set; }

    public decimal ChargeAmt { get; private set; }

    public decimal? FreeThresholdAmt { get; private set; }

    public string Courier { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>The single exchange policy: window in days and ordered plain-text conditions. Maps to STFR.XCHG_POLY and XCHG_COND.</summary>
public sealed class ExchangePolicy : Entity<long>, IAuditable
{
    private readonly List<ExchangeCondition> _conditions = [];

    private ExchangePolicy()
    {
    }

    public ExchangePolicy(int windowDays, IEnumerable<string> conditions)
    {
        WindowDays = windowDays;
        _conditions.AddRange(conditions.Select((c, i) => new ExchangeCondition(c, i + 1)));
    }

    public int WindowDays { get; private set; }

    public IReadOnlyList<ExchangeCondition> Conditions => _conditions;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>One plain-text exchange condition, in reading order. Maps to STFR.XCHG_COND.</summary>
public sealed class ExchangeCondition : Entity<long>, IAuditable
{
    private ExchangeCondition() => Text = string.Empty;

    public ExchangeCondition(string text, int sortSeq)
    {
        Text = (text ?? string.Empty).Trim();
        SortSeq = sortSeq;
    }

    public string Text { get; private set; }

    public int SortSeq { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>A popular search chip shown in the search overlay, in order. Maps to STFR.POPL_SRCH.</summary>
public sealed class PopularSearch : Entity<long>, IAuditable
{
    private PopularSearch() => Text = string.Empty;

    public PopularSearch(string text, int sortSeq)
    {
        Text = (text ?? string.Empty).Trim();
        SortSeq = sortSeq;
    }

    public string Text { get; private set; }

    public int SortSeq { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>The working copy of the settings. It carries a version counter that every save increments, so a stale save returns 409 (ST-14). Maps to STFR.DRFT_MAIN.</summary>
public sealed class SettingsDraft : AggregateRoot<long>, IAuditable
{
    public int Version { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Checks the version the caller loaded against the stored one, then moves on (ST-14).</summary>
    public void Advance(int expectedVersion)
    {
        if (expectedVersion != Version)
        {
            throw new DomainException("Someone else changed the settings since you opened them. Reload and apply your change again.");
        }

        Version++;
    }
}

/// <summary>
/// An immutable published version of the whole document the storefront reads (ADR 0020). The hash makes a second publish of unchanged
/// content a no-op. Maps to STFR.PUBL_SNAP.
/// </summary>
public sealed class PublishedSnapshot : AggregateRoot<long>, IAuditable
{
    private PublishedSnapshot()
    {
        Document = string.Empty;
        DocumentHash = string.Empty;
    }

    private PublishedSnapshot(int versionNo, string document, string note)
    {
        VersionNo = versionNo;
        Document = document;
        DocumentHash = HashOf(document);
        Note = note;
    }

    public int VersionNo { get; private set; }

    public string Document { get; private set; }

    /// <summary>Lowercase hex SHA-256 of the document (64 characters).</summary>
    public string DocumentHash { get; private set; }

    public bool IsCurrent { get; private set; }

    public string Note { get; private set; } = string.Empty;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public static PublishedSnapshot Create(int versionNo, string document, string? note)
    {
        if (Encoding.UTF8.GetByteCount(document) > SettingsLimits.MaxSnapshotBytes)
        {
            throw new DomainException("The settings are too large to publish (the limit is 512 KB). Remove some pages or entries.");
        }

        return new PublishedSnapshot(versionNo, document, (note ?? string.Empty).Trim()) { IsCurrent = true };
    }

    public void Retire() => IsCurrent = false;

    public static string HashOf(string document) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(document))).ToLowerInvariant();
}
