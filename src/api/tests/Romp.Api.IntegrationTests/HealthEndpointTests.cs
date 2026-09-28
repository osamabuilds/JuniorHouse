using System.Net;

namespace Romp.Api.IntegrationTests;

public sealed class HealthEndpointTests(RompApiWebApplicationFactory factory)
    : IClassFixture<RompApiWebApplicationFactory>
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoint_ReturnsOk(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
