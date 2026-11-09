using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Orders.Application.Checkout;

namespace Romp.Modules.Orders.Infrastructure.Payments;

/// <summary>
/// The production guard (PAY-15): the fake gateway may run only in Development and Testing. Anywhere else the host refuses to start with it
/// switched on, so a stand-in that accepts a forged "payment" can never take real orders. Runs at start through <c>ValidateOnStart</c>.
/// </summary>
internal sealed class OrdersGatewayOptionsValidator(IHostEnvironment environment) : IValidateOptions<OrdersOptions>
{
    public ValidateOptionsResult Validate(string? name, OrdersOptions options)
    {
        if (options.GatewayProvider == "fake" && !RompAuthorizationExtensions.IsDevelopmentLike(environment))
        {
            return ValidateOptionsResult.Fail("Orders:GatewayProvider 'fake' is only allowed in Development and Testing. Configure a real payment gateway, or leave it empty to sell Cash on Delivery only.");
        }

        if (options.GatewayProvider == "fake" && options.FakeGatewayWebhookSecret.Length is > 0 and < 16)
        {
            return ValidateOptionsResult.Fail("Orders:FakeGatewayWebhookSecret must be at least 16 characters.");
        }

        return ValidateOptionsResult.Success;
    }
}
