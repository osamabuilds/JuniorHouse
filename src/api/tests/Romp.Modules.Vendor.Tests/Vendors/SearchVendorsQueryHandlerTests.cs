using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Application.Vendors;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Tests.Support;

namespace Romp.Modules.Vendor.Tests.Vendors;

public sealed class SearchVendorsQueryHandlerTests
{
    [Fact]
    public async Task Handle_FilterBySpecialisation_ReturnsMatchingPage()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        await sender.Send(new CreateVendorCommand("Knits Vendor", "A", "1", null, 1, 1, SpecialisationIds: [1]));
        await sender.Send(new CreateVendorCommand("Wovens Vendor", "B", "2", null, 1, 1, SpecialisationIds: [2]));

        var knitVendors = await sender.Send(new SearchVendorsQuery(SpecialisationId: 1));

        Assert.Single(knitVendors);
        Assert.Equal("Knits Vendor", knitVendors[0].Name);
    }
}
