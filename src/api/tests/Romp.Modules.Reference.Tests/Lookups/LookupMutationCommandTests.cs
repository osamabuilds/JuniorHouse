using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Reference.Application;
using Romp.Modules.Reference.Application.Lookups;
using Romp.Modules.Reference.Domain;
using Romp.Modules.Reference.Domain.Lookups.Apparel;
using Romp.Modules.Reference.Domain.Lookups.Vendors;
using Romp.Modules.Reference.Tests.Support;

namespace Romp.Modules.Reference.Tests.Lookups;

/// <summary>SCRUM-172, AC-2: create/update/retire through the generic lookup commands.</summary>
public sealed class LookupMutationCommandTests
{
    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Retire_SetsActIndFalse_ExcludedFromDefaultList()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var created = await sender.Send(new CreateLookupCommand<ColourLookup>("TEAL", "Teal", null, 1));

        await sender.Send(new RetireLookupCommand<ColourLookup>(created.Id));

        var activeOnly = await sender.Send(new ListLookupQuery<ColourLookup>());
        var withInactive = await sender.Send(new ListLookupQuery<ColourLookup>(IncludeInactive: true));
        var retired = Assert.Single(withInactive);

        Assert.DoesNotContain(activeOnly, dto => dto.Id == created.Id);
        Assert.False(retired.IsActive);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Create_DuplicateCode_RejectedWithValidationError()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        await sender.Send(new CreateLookupCommand<CityLookup>("KHI", "Karachi", null, 1));

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(
            () => sender.Send(new CreateLookupCommand<CityLookup>("KHI", "Karachi Again", null, 2)));

        Assert.Contains("Code", exception.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Update_ValidEdit_UpdatesFieldsAndAppliesExtraColumn()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var created = await sender.Send(new CreateLookupCommand<PaymentTermLookup>(
            "NET15", "Net 15", null, 1, DefaultAdvancePercent: 10m));

        var updated = await sender.Send(new UpdateLookupCommand<PaymentTermLookup>(
            created.Id, "Net 15 Days", "Updated description", 2, DefaultAdvancePercent: 25m));

        Assert.Equal("Net 15 Days", updated.Name);
        Assert.Equal("Updated description", updated.Description);
        Assert.Equal((short)2, updated.SortSeq);
        Assert.Equal(25m, updated.DefaultAdvancePercent);
    }
}
