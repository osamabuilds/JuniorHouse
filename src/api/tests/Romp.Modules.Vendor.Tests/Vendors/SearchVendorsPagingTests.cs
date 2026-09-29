using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application.Vendors;
using Romp.Modules.Vendor.Tests.Support;

namespace Romp.Modules.Vendor.Tests.Vendors;

public sealed class SearchVendorsPagingTests
{
    [Fact]
    public async Task Handle_MoreVendorsThanPageSize_ReturnsOrderedPagesAndTotal()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        foreach (var name in new[] { "Cotton Co", "Aqua Mills", "Zed Knits", "Best Wovens", "Delta Dyes" })
        {
            await sender.Send(new CreateVendorCommand(name, "A", "1", null, 1, 1, SpecialisationIds: [1]));
        }

        var first = await sender.Send(new SearchVendorsQuery(Page: 1, PageSize: 2));
        var last = await sender.Send(new SearchVendorsQuery(Page: 3, PageSize: 2));

        Assert.Equal(5, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal(["Aqua Mills", "Best Wovens"], first.Items.Select(v => v.Name));
        Assert.Equal(["Zed Knits"], last.Items.Select(v => v.Name));
    }

    [Fact]
    public async Task Handle_PageOrSizeOutOfRange_IsClamped()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        await sender.Send(new CreateVendorCommand("Only Vendor", "A", "1", null, 1, 1, SpecialisationIds: [1]));

        var result = await sender.Send(new SearchVendorsQuery(Page: -4, PageSize: 100_000));

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Single(result.Items);
    }
}
