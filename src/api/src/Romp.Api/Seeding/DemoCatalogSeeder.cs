using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Domain.SizeCharts;
using Romp.Modules.Catalog.Infrastructure.Persistence;

namespace Romp.Api.Seeding;

/// <summary>
/// Sprint 7 (plan G11): one girls-dresses size chart for the sample sizes, so the "Try Sprint 7" walkthrough has a chart to show on
/// the sample dress. Products are not seeded: creating one from a labelled style is the walkthrough. Idempotent and Development only.
/// </summary>
internal static class DemoCatalogSeeder
{
    public static async Task SeedAsync(CatalogDbContext catalogContext, CancellationToken cancellationToken)
    {
        if (await catalogContext.Charts.AnyAsync(c => c.CategoryId == 3 && c.GenderId == 2, cancellationToken))
        {
            return;
        }

        catalogContext.Charts.Add(new SizeChart(3, 2, "Girls dresses", [
            new SizeChartLine(4, 12, 24, 80, 92, 9, 14, 52, 50, 42),
            new SizeChartLine(5, 24, 36, 92, 98, 12, 16, 54, 51, 46),
        ]));
        await catalogContext.SaveChangesAsync(cancellationToken);
    }
}
