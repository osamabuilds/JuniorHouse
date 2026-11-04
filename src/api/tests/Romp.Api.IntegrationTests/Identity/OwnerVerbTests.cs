using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.HostVerbs;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.Persistence;

namespace Romp.Api.IntegrationTests.Identity;

/// <summary>The operator verbs end to end through the real host services (staff-accounts AC-1, AC-12, AC-13): secrets from the environment, nothing printed that could leak one.</summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class OwnerVerbTests(ProductionApiFixture api)
{
    private const string Password = "operator-chosen-passphrase-42";
    private const string Replacement = "break-glass-new-passphrase-43";
    private static readonly int[] CatalogueRole = [2];

    private static OperatorSecretReader Reader(string userName, string password) => new(
        name => name switch
        {
            OwnerVerbs.UserNameVariable => userName,
            OwnerVerbs.DisplayNameVariable => "Operator Owner",
            OwnerVerbs.PasswordVariable => password,
            _ => null,
        },
        _ => null);

    private async Task NoOwnersAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        await db.Set<StaffUserRole>().Where(r => r.RoleId == 1).ExecuteDeleteAsync();
    }

    private static string NewName() => $"owner.{Guid.NewGuid():N}"[..20];

    [Fact]
    [Trait("Spec", "AC-1")]
    [Trait("Spec", "AC-12")]
    public async Task CreateOwner_RefusesASecondOwner_AndNeverPrintsThePassword()
    {
        await NoOwnersAsync();
        var first = new StringWriter();
        var second = new StringWriter();
        var name = NewName();

        var created = await HostVerbRunner.RunAsync(
            new HostVerbRequest(OwnerVerbs.CreateVerb, new HashSet<string>()),
            new Dictionary<string, HostVerbHandler> { [OwnerVerbs.CreateVerb] = (_, o, ct) => OwnerVerbs.CreateAsync(api.Services, Reader(name, Password), o, ct) },
            first, TextWriter.Null, CancellationToken.None);
        var refused = await OwnerVerbs.CreateAsync(api.Services, Reader(NewName(), Password), second, CancellationToken.None);

        Assert.Equal(0, created);
        Assert.Equal(HostVerbRunner.Failed, refused);
        Assert.Contains("already exists", second.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Password, first.ToString() + second, StringComparison.Ordinal);

        await using var scope = api.Services.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IdntDbContext>().Staff.AsNoTracking().SingleAsync(u => u.UserName == name);
        Assert.Equal(HostVerbRunner.Actor, owner.InsrBy);
        Assert.True(owner.MustChangePassword);
    }

    [Fact]
    [Trait("Spec", "AC-13")]
    public async Task ResetOwner_ChangesThePassword_AndRefusesNonOwners()
    {
        await NoOwnersAsync();
        var name = NewName();
        Assert.Equal(0, await OwnerVerbs.CreateAsync(api.Services, Reader(name, Password), TextWriter.Null, CancellationToken.None));
        var request = new HostVerbRequest(OwnerVerbs.ResetVerb, new HashSet<string> { "--password" });
        var output = new StringWriter();

        var reset = await OwnerVerbs.ResetAsync(api.Services, Reader(name, Replacement), request, output, CancellationToken.None);

        Assert.Equal(0, reset);
        Assert.DoesNotContain(Replacement, output.ToString(), StringComparison.Ordinal);
        await using var scope = api.Services.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<Romp.Modules.Identity.Application.StaffAccounts.IPasswordHasher>();
        var stored = await scope.ServiceProvider.GetRequiredService<IdntDbContext>().Staff.AsNoTracking().SingleAsync(u => u.UserName == name);
        Assert.True(hasher.Verify(new SecretString(Replacement), stored.PasswordHash));

        var nonOwner = NewName();
        var created = await api.Client.PostAsJsonAsyncCompat("/api/identity/staff", new { userName = nonOwner, displayName = "Not Owner", roleIds = CatalogueRole });
        Assert.True(created.IsSuccessStatusCode);
        var refused = await OwnerVerbs.ResetAsync(api.Services, Reader(nonOwner, Replacement), request, TextWriter.Null, CancellationToken.None);
        Assert.Equal(HostVerbRunner.Failed, refused);
    }

    [Fact]
    [Trait("Spec", "AC-13")]
    public async Task ResetOwner_WithoutAnyFlag_DoesNothing()
    {
        var output = new StringWriter();

        var exit = await OwnerVerbs.ResetAsync(
            api.Services, Reader(NewName(), Replacement), new HostVerbRequest(OwnerVerbs.ResetVerb, new HashSet<string>()), output, CancellationToken.None);

        Assert.Equal(HostVerbRunner.Failed, exit);
        Assert.Contains("Nothing to do", output.ToString(), StringComparison.Ordinal);
    }
}

internal static class HttpClientCompat
{
    public static Task<HttpResponseMessage> PostAsJsonAsyncCompat(this HttpClient client, string url, object body) =>
        System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(client, url, body, ProductionApiFixture.Json);
}
