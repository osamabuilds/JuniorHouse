using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Catalog.Application;
using Romp.Modules.Catalog.Application.Styles;
using Romp.Modules.Catalog.Tests.Support;

namespace Romp.Modules.Catalog.Tests.Styles;

public sealed class SearchStylesQueryHandlerTests
{
    [Fact]
    public async Task Handle_FilterByCategoryAndActive_ReturnsMatchingPage()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();

        await sender.Send(new CreateStyleCommand(
            "STY-A", "Denim Shorts", null, CategoryId: 2, 1, 1, 1, 300m, 900m,
            ColourIds: [1], SizeIds: [1], TargetLines: []));
        await sender.Send(new CreateStyleCommand(
            "STY-B", "Denim Jacket", null, CategoryId: 4, 1, 1, 1, 500m, 1500m,
            ColourIds: [1], SizeIds: [1], TargetLines: []));

        var categoryTwoOnly = await sender.Send(new SearchStylesQuery(CategoryId: 2));

        Assert.Single(categoryTwoOnly);
        Assert.Equal("STY-A", categoryTwoOnly[0].Code);
    }
}
