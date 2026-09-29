using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Vendor.Domain.Vendors;

/// <summary>Maps to VNDR.VNDR (FR-SC-01) - name, contact, city, specialisations, default payment term, scorecard.</summary>
public sealed class Vendor : AggregateRoot<long>, IAuditable
{
    private readonly List<VendorSpecialisation> _specialisations = [];

    private Vendor()
    {
        Name = string.Empty;
        ContactName = string.Empty;
        ContactPhone = string.Empty;
    }

    public Vendor(
        string name,
        string contactName,
        string contactPhone,
        string? contactEmail,
        short cityId,
        short paymentTermId)
    {
        Name = name;
        ContactName = contactName;
        ContactPhone = contactPhone;
        ContactEmail = contactEmail;
        CityId = cityId;
        PaymentTermId = paymentTermId;
        IsActive = true;
    }

    public string Name { get; private set; }

    public string ContactName { get; private set; }

    public string ContactPhone { get; private set; }

    public string? ContactEmail { get; private set; }

    public short CityId { get; private set; }

    public short PaymentTermId { get; private set; }

    /// <summary>Vendor scorecard (Sprint 4+) - null means "not yet measured", never confused with 0% (spec Decisions).</summary>
    public decimal? OnTimePercent { get; private set; }

    public decimal? OnQuantityPercent { get; private set; }

    public decimal? DefectRatePercent { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<VendorSpecialisation> Specialisations => _specialisations.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public void SetSpecialisations(IEnumerable<short> specialisationIds)
    {
        _specialisations.Clear();
        _specialisations.AddRange(specialisationIds.Distinct().Select(id => new VendorSpecialisation(Id, id)));
    }

    public void UpdateDetails(
        string contactName,
        string contactPhone,
        string? contactEmail,
        short cityId,
        short paymentTermId,
        bool isActive)
    {
        ContactName = contactName;
        ContactPhone = contactPhone;
        ContactEmail = contactEmail;
        CityId = cityId;
        PaymentTermId = paymentTermId;
        IsActive = isActive;
    }
}
