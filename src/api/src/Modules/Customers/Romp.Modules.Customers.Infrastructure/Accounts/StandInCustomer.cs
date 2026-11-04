using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Infrastructure.Persistence;

namespace Romp.Modules.Customers.Infrastructure.Accounts;

/// <summary>
/// The development-only stand-in for a signed-in customer (ADR 0021, decision 2). Real registration and sign-in are built in the final
/// authentication sprint; until then, in Development and Testing only, every request is the seeded test customer unless it carries
/// <c>X-Dev-Customer: none</c> (or <c>other</c>, the second seeded customer, for ownership checks) or the cookie set by <c>/dev/customer?state=off</c>. Nothing here is registered in any other environment.
/// </summary>
public static class StandInCustomer
{
    public const string Scheme = "DevelopmentCustomer";
    public const string Header = "X-Dev-Customer";
    public const string SwitchCookie = "romp_dev_customer";
    public const string Email = "dev.customer@example.test";
    public const string OtherEmail = "dev.other@example.test";

    public static bool IsAvailable(IHostEnvironment environment) => environment.IsDevelopment() || environment.IsEnvironment("Testing");

    /// <summary>Adds the two test customers (the stand-in and a second one for ownership checks) if they are missing. Safe to repeat.</summary>
    public static async Task SeedAsync(CustDbContext db, CancellationToken cancellationToken)
    {
        foreach (var (email, first, last) in new[] { (Email, "Dev", "Customer"), (OtherEmail, "Other", "Customer") })
        {
            if (!await db.Customers.AnyAsync(c => c.Email == email, cancellationToken))
            {
                db.Customers.Add(Customer.Create(email, first, last));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static void MapSwitch(IEndpointRouteBuilder routes)
    {
        // Lets a browser turn the stand-in off and on, so the guest flows can be tried by hand. Absent outside Development and Testing.
        routes.MapGet("/dev/customer", (string? state, HttpContext http) =>
        {
            if (state is "off")
            {
                http.Response.Cookies.Append(SwitchCookie, "off", new CookieOptions { SameSite = SameSiteMode.Lax, HttpOnly = true, Path = "/" });
            }
            else if (state is "on")
            {
                http.Response.Cookies.Delete(SwitchCookie, new CookieOptions { Path = "/" });
            }
            else
            {
                return Results.BadRequest(new { error = "state must be on or off" });
            }

            return Results.NoContent();
        }).AllowAnonymous().WithTags("Development");
    }
}

internal sealed class StandInCustomerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers[StandInCustomer.Header] == "none" || Request.Cookies[StandInCustomer.SwitchCookie] == "off")
        {
            return AuthenticateResult.NoResult();
        }

        var db = Context.RequestServices.GetRequiredService<ICustomerDbContext>();
        var email = Request.Headers[StandInCustomer.Header] == "other" ? StandInCustomer.OtherEmail : StandInCustomer.Email;
        var id = await db.Customers.AsNoTracking().Where(c => c.Email == email).Select(c => (long?)c.Id).FirstOrDefaultAsync(Context.RequestAborted);
        if (id is null)
        {
            return AuthenticateResult.NoResult();
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))], StandInCustomer.Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), StandInCustomer.Scheme));
    }
}

/// <summary>
/// Reads the customer from the authenticated principal (never from the route or body). Only an identity made by a customer scheme (the real
/// session or the development stand-in) counts: a staff session is never a customer. <see cref="ResolveAsync"/> runs the customer scheme for
/// routes that are open to guests, which the host does not authenticate on its own.
/// </summary>
internal sealed class HttpContextCurrentCustomer(IHttpContextAccessor accessor, IOptions<CustomerAuthOptions> auth, IHostEnvironment environment) : ICurrentCustomer
{
    public long? CustomerId => FromPrincipal(accessor.HttpContext?.User);

    public async Task<long?> ResolveAsync(CancellationToken cancellationToken)
    {
        var http = accessor.HttpContext;
        if (http is null)
        {
            return null;
        }

        if (FromPrincipal(http.User) is { } known)
        {
            return known;
        }

        var scheme = CustomerAuthOptions.IsReal(auth.Value, environment) ? CustomerSessionPrincipal.Scheme : StandInCustomer.Scheme;
        var result = await http.AuthenticateAsync(scheme);
        return result.Succeeded ? FromPrincipal(result.Principal) : null;
    }

    private static long? FromPrincipal(ClaimsPrincipal? user)
    {
        var identity = user?.Identities.FirstOrDefault(
            i => i.IsAuthenticated && i.AuthenticationType is StandInCustomer.Scheme or CustomerSessionPrincipal.Scheme);
        var value = identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
    }
}
