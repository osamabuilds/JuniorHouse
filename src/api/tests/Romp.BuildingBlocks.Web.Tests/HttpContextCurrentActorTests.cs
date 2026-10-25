using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Web;

namespace Romp.BuildingBlocks.Web.Tests;

public sealed class HttpContextCurrentActorTests
{
    private static HttpContextCurrentActor ActorFor(HttpContext? context) =>
        new(new HttpContextAccessor { HttpContext = context });

    private static DefaultHttpContext SignedIn(string name)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "test");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public void UserName_OutsideARequest_IsSystem()
    {
        Assert.Equal("system", ActorFor(null).UserName);
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public void UserName_AnonymousRequest_IsSystem()
    {
        Assert.Equal("system", ActorFor(new DefaultHttpContext()).UserName);
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public void UserName_SignedInRequest_IsThePrincipalName()
    {
        Assert.Equal("jane.doe", ActorFor(SignedIn("jane.doe")).UserName);
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public void UserName_ActorScopeWinsOverThePrincipal()
    {
        using var scope = ActorScope.Begin("operator-cli");

        Assert.Equal("operator-cli", ActorFor(SignedIn("jane.doe")).UserName);
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public void UserName_ActorScopeApplies_EvenOutsideARequest()
    {
        using var scope = ActorScope.Begin("operator-cli");

        Assert.Equal("operator-cli", ActorFor(null).UserName);
    }

    [Theory]
    [InlineData("Jane Doe")]
    [InlineData("jane\nforged-entry")]
    [InlineData("a;b")]
    public void UserName_PrincipalNameOutsideTheCharset_NeverReachesTheAuditColumn(string hostile)
    {
        var actual = ActorFor(SignedIn(hostile)).UserName;

        Assert.Equal("unknown", actual);
    }

    [Fact]
    public void UserName_OverlongPrincipalName_IsRejected()
    {
        var actual = ActorFor(SignedIn(new string('a', 101))).UserName;

        Assert.Equal("unknown", actual);
    }
}
