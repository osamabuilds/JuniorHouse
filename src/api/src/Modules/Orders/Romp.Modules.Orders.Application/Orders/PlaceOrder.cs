using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Cart.Contracts;
using Romp.Modules.Customers.Contracts;
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Application.Payments;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Orders.Application.Orders;

/// <summary>
/// Contact details typed at checkout (or taken from the account). Required for the order record whatever the account state (CHK-6, CHK-7);
/// nothing here is verified, and nothing blocks an order (owner decision).
/// </summary>
public sealed record CheckoutContact(string? Name, string? Email, string? Mobile, bool WhatsAppOptIn);

/// <summary>A delivery address typed at checkout. Ignored when <see cref="PlaceOrderCommand.SavedAddressId"/> is used.</summary>
public sealed record CheckoutAddress(string? RecipientName, string? RecipientPhone, string? LineOne, string? LineTwo, short ProvinceId, short CityId, string? Postcode, string? Note);

/// <summary>
/// Place the order for the caller's basket (ORD-1 to ORD-12, ADR 0025). Nothing in this command carries a price, a total or a discount:
/// those come from the server (SEC-09). <see cref="IdempotencyKey"/> is made once per checkout page load, so a double click or a Back and
/// resubmit gives the same order (CHK-10).
/// </summary>
public sealed record PlaceOrderCommand(
    string IdempotencyKey,
    CheckoutContact Contact,
    CheckoutAddress? Address,
    long? SavedAddressId,
    short PaymentMethodId) : IRequest<PlaceOrderResult>;

public enum PlaceOrderOutcome
{
    /// <summary>The order exists (new, or the earlier one for a repeated key).</summary>
    Placed,

    /// <summary>Nothing was placed; <see cref="PlaceOrderResult.Refusal"/> says why and the basket is untouched.</summary>
    Refused,
}

public enum PlaceOrderRefusal
{
    None,
    EmptyBasket,
    BasketProblems,
    OutOfStock,
    CodeRejected,
    AddressNotFound,
}

public sealed record PlaceOrderResult(
    PlaceOrderOutcome Outcome,
    long? OrderId,
    string? OrderNumber,
    bool WasRepeat,
    PlaceOrderRefusal Refusal,
    IReadOnlyList<string> Messages,
    IReadOnlyList<long> ShortVariantIds,
    string? ConfirmToken = null,
    string? PayToken = null)
{
    public static PlaceOrderResult Placed(long id, string number, bool repeat, string? confirmToken = null, string? payToken = null) => new(PlaceOrderOutcome.Placed, id, number, repeat, PlaceOrderRefusal.None, [], [], confirmToken, payToken);

    public static PlaceOrderResult Refused(PlaceOrderRefusal why, IReadOnlyList<string> messages, IReadOnlyList<long>? shortVariants = null) =>
        new(PlaceOrderOutcome.Refused, null, null, false, why, messages, shortVariants ?? []);
}

public sealed partial class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public const int MinKeyLength = 16;
    public const int MaxKeyLength = 64;

    [System.Text.RegularExpressions.GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial System.Text.RegularExpressions.Regex KeyPattern();

    public PlaceOrderCommandValidator()
    {
        RuleFor(c => c.IdempotencyKey).NotEmpty().Length(MinKeyLength, MaxKeyLength).Must(k => KeyPattern().IsMatch(k)).WithMessage("The submit key is not valid. Please refresh the page and try again.");
        RuleFor(c => c.PaymentMethodId).Must(PaymentMethods.IsKnown).WithMessage("Choose a payment method.");
        RuleFor(c => c.Contact).NotNull();
        RuleFor(c => c.Contact.Name).NotEmpty().WithMessage("Enter your name.").MaximumLength(100).When(c => c.Contact is not null);
        RuleFor(c => c.Contact.Mobile).NotEmpty().WithMessage("Enter your mobile number.")
            .Must(m => PhoneNumber.TryNormaliseMobile(m, out _)).WithMessage("Enter a Pakistani mobile number such as 0300 1234567.").When(c => c.Contact is not null);
        RuleFor(c => c.Contact.Email).MaximumLength(254).EmailAddress().When(c => c.Contact is not null && !string.IsNullOrWhiteSpace(c.Contact.Email));
        RuleFor(c => c.Address).NotNull().WithMessage("Enter a delivery address.").When(c => c.SavedAddressId is null);
        When(c => c.SavedAddressId is null && c.Address is not null, () =>
        {
            RuleFor(c => c.Address!.RecipientName).NotEmpty().WithMessage("Enter the name of the person receiving the parcel.").MaximumLength(100);
            RuleFor(c => c.Address!.RecipientPhone).NotEmpty().WithMessage("Enter a phone number for the courier.")
                .Must(p => PhoneNumber.TryNormalise(p, out _)).WithMessage("Enter a phone number the courier can call.");
            RuleFor(c => c.Address!.LineOne).NotEmpty().WithMessage("Enter the street address.").MaximumLength(200);
            RuleFor(c => c.Address!.LineTwo).MaximumLength(200);
            RuleFor(c => c.Address!.Postcode).MaximumLength(10);
            RuleFor(c => c.Address!.Note).MaximumLength(200);
            RuleFor(c => c.Address!.CityId).GreaterThan((short)0).WithMessage("Choose a city.");
            RuleFor(c => c.Address!.ProvinceId).GreaterThan((short)0).WithMessage("Choose a province.");
        });
    }
}

public sealed partial class PlaceOrderCommandHandler(
    IOrdersDbContext dbContext,
    ISender sender,
    ICheckoutPricing pricing,
    IDiscountRedemptions redemptions,
    IStockReservations reservations,
    ICodLinks links,
    IPaymentLinks paymentLinks,
    TimeProvider clock,
    ICurrentCustomerAccessor customers,
    ICustomerAddressQueries savedAddresses,
    IGeographyQueries geography,
    ILogger<PlaceOrderCommandHandler> logger) : IRequestHandler<PlaceOrderCommand, PlaceOrderResult>
{
    public async Task<PlaceOrderResult> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        // 1-2. The key, tied to the caller, and the earlier order for it, if there is one (ORD-1).
        var callerKey = await pricing.GetCallerKeyAsync(cancellationToken);
        if (callerKey is null)
        {
            // No basket belongs to this caller: it is empty, or the session that held it timed out. Nothing is placed or charged either way (CHK-5).
            return PlaceOrderResult.Refused(PlaceOrderRefusal.EmptyBasket, ["Your bag is empty. If you were signed in, your session may have timed out: please refresh the page or sign in again."]);
        }

        var placementKey = DerivePlacementKey(callerKey, request.IdempotencyKey);
        var earlier = await FindAsync(placementKey, cancellationToken);
        if (earlier is not null)
        {
            return PlaceOrderResult.Placed(earlier.Value.Id, earlier.Value.Number, repeat: true, TokenFor(request, earlier.Value.Number), PayTokenFor(request, earlier.Value.Number));
        }

        // The address: a saved one (the caller's own, SEC-05) or the typed one, always checked against the geography lookups (CHK-8).
        var customerId = await customers.ResolveAsync(cancellationToken);
        var address = await ResolveAddressAsync(request, customerId, cancellationToken);
        if (address.Refusal is not null)
        {
            return address.Refusal;
        }

        var contact = new OrderContact(
            request.Contact.Name!.Trim(),
            string.IsNullOrWhiteSpace(request.Contact.Email) ? null : request.Contact.Email.Trim(),
            PhoneNumber.Normalise(request.Contact.Mobile),
            request.Contact.WhatsAppOptIn);

        // 3. The server prices the basket: every figure comes from here (SEC-09, ORD-4).
        var isCod = PaymentMethods.IsCashOnDelivery(request.PaymentMethodId);
        var priced = await pricing.PriceCurrentCartAsync(address.Value!.CityId, isCod, cancellationToken);
        if (priced.Lines.Count == 0 && priced.Problems.Count > 0 && priced.Subtotal <= 0)
        {
            return PlaceOrderResult.Refused(PlaceOrderRefusal.EmptyBasket, priced.Problems);
        }

        if (!priced.CanOrder)
        {
            return PlaceOrderResult.Refused(PlaceOrderRefusal.BasketProblems, priced.Problems);
        }

        // 4. The discount hold, if a code is applied (cheapest to fail first).
        var mobileHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contact.Mobile))).ToLowerInvariant();
        var hold = await redemptions.HoldForCurrentCartAsync(placementKey, customerId is null ? mobileHash : null, cancellationToken);
        if (hold.Refusal is not null)
        {
            return PlaceOrderResult.Refused(PlaceOrderRefusal.CodeRejected, [hold.Refusal]);
        }

        if (hold.Applied && hold.Amount != priced.Discount)
        {
            await TryReleaseDiscountAsync(placementKey);
            return PlaceOrderResult.Refused(PlaceOrderRefusal.CodeRejected, ["Your discount has changed. Please review your bag and try again."]);
        }

        // 5. The stock reservation, all lines or none (ORD-2, ORD-3).
        var reserved = await reservations.ReserveAsync(placementKey, [.. priced.Lines.Select(l => new ReservationLineRequest(l.StockVariantId, l.Quantity))], cancellationToken);
        if (reserved.Outcome != ReservationOutcome.Reserved)
        {
            await TryReleaseDiscountAsync(placementKey);
            var names = reserved.Shortages
                .Select(s => priced.Lines.FirstOrDefault(l => l.StockVariantId == s.VariantId) is { } line
                    ? (s.Available <= 0 ? $"{line.Name} ({line.Colour}, {line.Size}) is out of stock." : $"Only {s.Available} of {line.Name} ({line.Colour}, {line.Size}) are left.")
                    : "An item in your bag is no longer available.")
                .ToList();
            return PlaceOrderResult.Refused(PlaceOrderRefusal.OutOfStock, names.Count > 0 ? names : ["Some items are no longer available."], [.. reserved.Shortages.Select(s => priced.Lines.FirstOrDefault(l => l.StockVariantId == s.VariantId)?.VariantId ?? 0).Where(id => id != 0)]);
        }

        // 6. The order, in one transaction of its own (ORD-7). A twin that won the race returns its order; any other failure gives everything back.
        try
        {
            var data = new CreateOrderCommand(
                placementKey, customerId, contact, address.Value!, priced, hold.Applied ? hold.Code : null, request.PaymentMethodId);
            var created = await sender.Send(data, cancellationToken);
            return PlaceOrderResult.Placed(created.OrderId, created.OrderNumber, repeat: false, TokenFor(request, created.OrderNumber), PayTokenFor(request, created.OrderNumber));
        }
        catch (DbUpdateException)
        {
            var twin = await FindAsync(placementKey, cancellationToken);
            if (twin is not null)
            {
                return PlaceOrderResult.Placed(twin.Value.Id, twin.Value.Number, repeat: true, TokenFor(request, twin.Value.Number), PayTokenFor(request, twin.Value.Number));
            }

            await CompensateAsync(placementKey);
            throw;
        }
        catch
        {
            await CompensateAsync(placementKey);
            throw;
        }
    }

    /// <summary>A cash on delivery order gets a signed confirm link for its page button and email (COD-12); other orders get none.</summary>
    private string? TokenFor(PlaceOrderCommand request, string orderNumber) =>
        PaymentMethods.IsCashOnDelivery(request.PaymentMethodId) ? links.Create(orderNumber, clock.GetUtcNow()) : null;

    /// <summary>An online order gets a signed token that lets its shopper start and follow the payment of that one order, with no account needed (PAY-3).</summary>
    private string? PayTokenFor(PlaceOrderCommand request, string orderNumber) =>
        PaymentMethods.IsCashOnDelivery(request.PaymentMethodId) ? null : paymentLinks.Create(orderNumber, clock.GetUtcNow());

    /// <summary>The same person sending the same key gets the same placement key; a different person never collides with it (ADR 0025).</summary>
    public static string DerivePlacementKey(string callerKey, string idempotencyKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{callerKey}|{idempotencyKey}"))).ToLowerInvariant();

    private async Task<(long Id, string Number)?> FindAsync(string placementKey, CancellationToken cancellationToken)
    {
        var found = await dbContext.Orders.AsNoTracking().Where(o => o.PlacementKey == placementKey).Select(o => new { o.Id, o.OrderNumber }).FirstOrDefaultAsync(cancellationToken);
        return found is null ? null : (found.Id, found.OrderNumber);
    }

    private async Task<(OrderAddress? Value, PlaceOrderResult? Refusal)> ResolveAddressAsync(PlaceOrderCommand request, long? customerId, CancellationToken cancellationToken)
    {
        string recipient, phone, lineOne;
        string? lineTwo, postcode, note;
        short provinceId, cityId;

        if (request.SavedAddressId is { } savedId)
        {
            // Another customer's address, or a guest's attempt, behaves exactly like a missing one (SEC-05).
            var saved = customerId is null ? null : await savedAddresses.GetMyAddressAsync(savedId, cancellationToken);
            if (saved is null)
            {
                return (null, PlaceOrderResult.Refused(PlaceOrderRefusal.AddressNotFound, ["Choose a delivery address."]));
            }

            (recipient, phone, lineOne, lineTwo, postcode, note, provinceId, cityId) = (saved.RecipientName, saved.Phone, saved.LineOne, saved.LineTwo, saved.Postcode, request.Address?.Note, saved.ProvinceId, saved.CityId);
        }
        else
        {
            var a = request.Address!;
            (recipient, phone, lineOne, lineTwo, postcode, note, provinceId, cityId) =
                (a.RecipientName!.Trim(), PhoneNumber.Normalise(a.RecipientPhone), a.LineOne!.Trim(), Trim(a.LineTwo), Trim(a.Postcode), Trim(a.Note), a.ProvinceId, a.CityId);
        }

        if (!await geography.CityBelongsToProvinceAsync(cityId, provinceId, cancellationToken))
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]> { ["cityId"] = ["Choose a city that belongs to the province."] });
        }

        var cities = await geography.GetCitiesByIdAsync([cityId], cancellationToken);
        var provinces = await geography.GetProvincesByIdAsync([provinceId], cancellationToken);
        return (new OrderAddress(recipient, phone, lineOne, lineTwo, cityId, cities[cityId].Name, provinceId, provinces[provinceId].Name, postcode, note), null);

        static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task TryReleaseDiscountAsync(string placementKey)
    {
        try
        {
            await redemptions.ReleaseAsync(placementKey, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // The hold sweeper gives it back within HoldMinutes (ADR 0025 step 7), so a failure here is only logged.
            LogDiscountReleaseFailed(ex);
        }
    }

    private async Task CompensateAsync(string placementKey)
    {
        await TryReleaseDiscountAsync(placementKey);
        try
        {
            await reservations.ReleaseAsync(placementKey, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogReservationReleaseFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Releasing a discount hold failed; the sweeper will release it.")]
    private partial void LogDiscountReleaseFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Releasing a stock reservation failed; the sweeper will release it.")]
    private partial void LogReservationReleaseFailed(Exception exception);
}
