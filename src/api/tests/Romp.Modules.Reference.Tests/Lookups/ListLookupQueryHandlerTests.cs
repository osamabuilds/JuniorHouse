using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Reference.Application.Lookups;
using Romp.Modules.Reference.Domain.Lookups.Apparel;
using Romp.Modules.Reference.Tests.Support;

namespace Romp.Modules.Reference.Tests.Lookups;

/// <summary>AC-1, AC-2: a retired lookup row is excluded from the default list but still readable with includeInactive.</summary>
public sealed class ListLookupQueryHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-1")]
    [Trait("Spec", "AC-2")]
    public async Task Handle_DefaultsToActiveOnly_IncludesInactiveWhenRequested()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();

        var first = await sender.Send(new CreateLookupCommand<SizeLookup>("XS", "Extra Small", null, 1));
        await sender.Send(new CreateLookupCommand<SizeLookup>("SM", "Small", null, 2));
        await sender.Send(new RetireLookupCommand<SizeLookup>(first.Id));

        var activeOnly = await sender.Send(new ListLookupQuery<SizeLookup>());
        var includingInactive = await sender.Send(new ListLookupQuery<SizeLookup>(IncludeInactive: true));

        Assert.Single(activeOnly);
        Assert.Equal("SM", activeOnly[0].Code);
        Assert.Equal(2, includingInactive.Count);
        Assert.Contains(includingInactive, dto => dto.Code == "XS" && !dto.IsActive);
    }
}
