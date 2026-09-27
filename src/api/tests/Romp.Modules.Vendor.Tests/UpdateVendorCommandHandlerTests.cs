using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application;

namespace Romp.Modules.Vendor.Tests;

public sealed class UpdateVendorCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task Handle_ValidEdit_UpdatesAuditColumns()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var created = await sender.Send(new CreateVendorCommand(
            "Lahore Knits", "Bilal Khan", "+92-300-7654321", null, CityId: 2, PaymentTermId: 1, SpecialisationIds: [1]));

        var updated = await sender.Send(new UpdateVendorCommand(
            created.Id, "Bilal Khan Jr.", "+92-300-0000000", "bilal@lk.pk", CityId: 3, PaymentTermId: 2,
            SpecialisationIds: [1, 2], IsActive: false));

        Assert.Equal("Bilal Khan Jr.", updated.ContactName);
        Assert.Equal((short)3, updated.CityId);
        Assert.False(updated.IsActive);
        Assert.Equal(2, updated.SpecialisationIds.Count);

        var reloaded = await sender.Send(new GetVendorByIdQuery(created.Id));
        Assert.NotNull(reloaded);
        Assert.Equal("Bilal Khan Jr.", reloaded.ContactName);
    }
}
