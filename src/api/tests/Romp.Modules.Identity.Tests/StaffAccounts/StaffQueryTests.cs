using System.Reflection;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.StaffAccounts;

[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class ListStaffQueryTests(IdentityPostgres postgres)
{
    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task Pages_InOneQuery()
    {
        var counter = new CommandCounter();
        using var host = new IdentityHost(postgres, counter);
        var catalogue = await host.RoleIdAsync(RoleCodes.Catalogue);
        var support = await host.RoleIdAsync(RoleCodes.Support);
        for (var i = 0; i < 5; i++)
        {
            await host.SendAsync(new CreateStaffCommand(IdentityHost.NewUserName(), "Test Person", [catalogue, support]));
        }

        counter.Reset();
        var small = await host.SendAsync(new ListStaffQuery(Page: 1, PageSize: 2));
        var queriesForTwoRows = counter.Count;

        counter.Reset();
        var large = await host.SendAsync(new ListStaffQuery(Page: 1, PageSize: 100));
        var queriesForManyRows = counter.Count;

        Assert.Equal(2, small.Items.Count);
        Assert.True(large.Items.Count >= 5);
        Assert.Equal(queriesForTwoRows, queriesForManyRows);
        Assert.True(queriesForManyRows <= 2, $"one COUNT and one page query, got {queriesForManyRows}");
        Assert.All(large.Items.Where(i => i.Roles.Count > 0), i => Assert.All(i.Roles, r => Assert.False(string.IsNullOrEmpty(r.Name))));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task Pages_DoNotOverlap_AndSearchFiltersByNameOrDisplayName()
    {
        using var host = new IdentityHost(postgres);
        var role = await host.RoleIdAsync(RoleCodes.Catalogue);
        var tag = $"zq{Environment.TickCount64 % 100000}";
        for (var i = 0; i < 3; i++)
        {
            await host.SendAsync(new CreateStaffCommand($"{tag}.{i}", "Search Person", [role]));
        }

        var first = await host.SendAsync(new ListStaffQuery(Search: tag, Page: 1, PageSize: 2));
        var second = await host.SendAsync(new ListStaffQuery(Search: tag, Page: 2, PageSize: 2));

        Assert.Equal(3, first.TotalCount);
        Assert.Equal(2, first.Items.Count);
        Assert.Single(second.Items);
        Assert.Empty(first.Items.Select(i => i.Id).Intersect(second.Items.Select(i => i.Id)));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task PageSize_AboveOneHundred_IsRefused()
    {
        using var host = new IdentityHost(postgres);

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => host.SendAsync(new ListStaffQuery(Page: 1, PageSize: 101)));
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => host.SendAsync(new ListStaffQuery(Page: 0, PageSize: 10)));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task GetStaff_ReturnsTheAccountWithItsRoles_AndThrowsWhenMissing()
    {
        using var host = new IdentityHost(postgres);
        var role = await host.RoleIdAsync(RoleCodes.Catalogue);
        var created = await StaffHandlerHelpers.CreateAsync(host, role);

        var dto = await host.SendAsync(new GetStaffQuery(created.UserId));

        Assert.Equal(created.UserName, dto.UserName);
        Assert.True(dto.IsActive);
        Assert.True(dto.MustChangePassword);
        Assert.Equal(created.ExpiresAt, dto.TemporaryPasswordExpiresAt);
        Assert.Equal([RoleCodes.Catalogue], dto.Roles.Select(r => r.Code));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new GetStaffQuery(long.MaxValue)));
    }
}

public sealed class StaffDtoTests
{
    private static readonly string[] Forbidden = ["hash", "secret", "password", "token"];

    [Fact]
    [Trait("Spec", "AC-3")]
    public void NeverCarriesSecrets()
    {
        var dtoTypes = typeof(StaffDto).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(StaffDto).Namespace && t.Name.EndsWith("Dto", StringComparison.Ordinal));

        foreach (var type in dtoTypes)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Dates and flags about a temporary password are fine; a value that could be the password or its hash is not.
                var risky = Forbidden.Any(word => property.Name.Contains(word, StringComparison.OrdinalIgnoreCase));
                var harmless = property.PropertyType == typeof(bool) || property.PropertyType == typeof(DateTimeOffset)
                    || property.PropertyType == typeof(DateTimeOffset?);
                Assert.True(!risky || harmless, $"{type.Name}.{property.Name} could carry a secret");
                Assert.NotEqual(typeof(Romp.Modules.Identity.Domain.StaffAccounts.SecretString), property.PropertyType);
            }
        }
    }
}
