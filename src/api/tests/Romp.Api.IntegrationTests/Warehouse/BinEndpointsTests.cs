using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Romp.Api.IntegrationTests.Production;

namespace Romp.Api.IntegrationTests.Warehouse;

/// <summary>Warehouse bins through the real HTTP API against a real PostgreSQL: routes, status codes, secured group, problem details.</summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class BinEndpointsTests(ProductionApiFixture api)
{
    private const string Person = "Ayesha Khan";

    private static int _zone;

    private static string UniqueZone() => "W" + (Interlocked.Increment(ref _zone) % 256).ToString("X2", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    [Trait("Spec", "BIN-1")]
    [Trait("Spec", "BIN-6")]
    public async Task ANewBin_IsCreated_FoundByItsPrintedCode_AndListed()
    {
        var zone = UniqueZone();

        var created = await ProductionApiFixture.ReadAsync<JsonElement>(
            await api.PostAsync("/api/warehouse/bins", new { zone, aisle = "04", shelf = "b", actingPerson = Person }));
        var code = created.GetProperty("binCode").GetString();
        Assert.Equal($"{zone}-04-B", code);

        var byCode = await api.Client.GetAsync($"/api/warehouse/bins/by-code/{code!.ToLowerInvariant()}");
        Assert.Equal(HttpStatusCode.OK, byCode.StatusCode);

        var list = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/warehouse/bins?zone={zone}&pageSize=10"));
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    [Trait("Spec", "BIN-2")]
    public async Task ADuplicateCode_IsRefused_As409_WithProblemDetails()
    {
        var zone = UniqueZone();
        Assert.True((await api.PostAsync("/api/warehouse/bins", new { zone, aisle = "01", shelf = "A", actingPerson = Person })).IsSuccessStatusCode);

        var duplicate = await api.PostAsync("/api/warehouse/bins", new { zone, aisle = "01", shelf = "A", actingPerson = Person });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("already exists", await duplicate.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("A'; DROP TABLE", "01", "A")]
    [InlineData("A", "xx", "A")]
    [InlineData("A", "01", "<b>")]
    [Trait("Spec", "BIN-3")]
    public async Task APartOutsideThePattern_Is400_WithAFieldError(string zone, string aisle, string shelf)
    {
        var response = await api.PostAsync("/api/warehouse/bins", new { zone, aisle, shelf, actingPerson = Person });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Spec", "BIN-7")]
    public async Task WithoutTheActingPerson_Is400() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsync("/api/warehouse/bins", new { zone = "A", aisle = "01", shelf = "A", actingPerson = "" })).StatusCode);

    [Fact]
    [Trait("Spec", "BIN-5")]
    public async Task AnUnknownCode_Is404_AndAnUnknownBinId_Is404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync("/api/warehouse/bins/by-code/NO-1-X")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.PostAsync("/api/warehouse/bins/999999999/active", new { active = false, actingPerson = Person })).StatusCode);
    }

    [Fact]
    [Trait("Spec", "BIN-4")]
    public async Task AnEmptyBin_CanBeDeactivatedAndReactivated_OverHttp()
    {
        var created = await ProductionApiFixture.ReadAsync<JsonElement>(
            await api.PostAsync("/api/warehouse/bins", new { zone = UniqueZone(), aisle = "09", shelf = "C", actingPerson = Person }));
        var id = created.GetProperty("id").GetInt64();

        var off = await ProductionApiFixture.ReadAsync<JsonElement>(await api.PostAsync($"/api/warehouse/bins/{id}/active", new { active = false, actingPerson = Person }));
        var on = await ProductionApiFixture.ReadAsync<JsonElement>(await api.PostAsync($"/api/warehouse/bins/{id}/active", new { active = true, actingPerson = Person }));

        Assert.False(off.GetProperty("isActive").GetBoolean());
        Assert.True(on.GetProperty("isActive").GetBoolean());
    }
}
