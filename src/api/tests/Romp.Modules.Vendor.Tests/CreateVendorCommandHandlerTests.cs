using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application;

namespace Romp.Modules.Vendor.Tests;

public sealed class CreateVendorCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-5")]
    [Trait("Spec", "AC-5a")]
    public async Task Handle_MultipleSpecialisations_PersistsAllInMap()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();

        var result = await sender.Send(new CreateVendorCommand(
            "Sialkot Garments Co.", "Ali Raza", "+92-300-1234567", "ali@sgc.pk",
            CityId: 7, PaymentTermId: 2, SpecialisationIds: [1, 3]));

        Assert.True(result.Id > 0);
        Assert.True(result.IsActive);
        Assert.Null(result.OnTimePercent);
        Assert.Equal(2, result.SpecialisationIds.Count);
        Assert.Contains((short)1, result.SpecialisationIds);
        Assert.Contains((short)3, result.SpecialisationIds);
    }
}
