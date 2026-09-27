using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Romp.Api.IntegrationTests;

/// <summary>
/// Boots the API host under a "Testing" environment instead of "Development", so Program.cs's
/// dev-only startup migrations never run here against a real database. CLAUDE.md reserves real
/// PostgreSQL for tests that actually exercise migrations/queries, via Testcontainers - this
/// fixture is for tests that only need to prove the DI container wires up correctly.
/// </summary>
public sealed class RompApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
}
