using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Romp.Modules.Shipping.Application.Abstractions;
using Romp.Modules.Shipping.Application.CourierPort;
using Romp.Modules.Shipping.Domain;

namespace Romp.Modules.Shipping.Infrastructure.CourierPort;

/// <summary>
/// What the pretend couriers remember (ADR 0031): which of them are "down", the waybills they issued and the status each is scripted to report. Shared
/// by every fake gateway; registered only in Development and Testing, like the pretend payment gateway.
/// </summary>
public sealed class FakeCourierState
{
    private long _counter;

    public ConcurrentDictionary<short, bool> Outage { get; } = new();

    public ConcurrentDictionary<string, string> Issued { get; } = new();

    /// <summary>The status a waybill is scripted to report when asked (polling), with its courier event id.</summary>
    public ConcurrentDictionary<string, CourierStatus> Script { get; } = new();

    public string NextWaybill(string prefix) => $"{prefix}-{Interlocked.Increment(ref _counter):000000}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}

/// <summary>
/// A pretend courier (CPT-6): it books in memory, can be switched "down", refuses an address that contains the word REJECT, answers status from a
/// script, and signs and verifies status pushes with an HMAC-SHA256 over the raw body. Never registered in production.
/// </summary>
public sealed class FakeCourierGateway(short courierId, string secret, bool pushesStatus, FakeCourierState state) : ICourierGateway
{
    public const string SignatureHeader = "X-Courier-Signature";

    public short CourierId => courierId;

    public bool PushesStatus => pushesStatus;

    public Task<BookingResult> BookAsync(BookingRequest request, CancellationToken cancellationToken)
    {
        if (state.Outage.TryGetValue(courierId, out var down) && down)
        {
            return Task.FromResult(BookingResult.Unreachable());
        }

        if (request.AddressLine1.Contains("REJECT", StringComparison.OrdinalIgnoreCase) || request.City.Contains("REJECT", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(BookingResult.Rejected("The address is not serviceable."));
        }

        var waybill = state.NextWaybill(courierId == Couriers.Primary ? "PRM" : "SEC");
        state.Issued[waybill] = request.OrderNumber;
        return Task.FromResult(BookingResult.Booked(waybill));
    }

    public Task<CourierStatus?> StatusAsync(string waybill, CancellationToken cancellationToken)
    {
        if (state.Outage.TryGetValue(courierId, out var down) && down)
        {
            return Task.FromResult<CourierStatus?>(null);
        }

        return Task.FromResult(state.Script.TryGetValue(waybill, out var status) ? status : null);
    }

    public Task<bool> CancelAsync(string waybill, CancellationToken cancellationToken) => Task.FromResult(state.Issued.TryRemove(waybill, out _));

    public CourierPush? ParsePush(string body, string? signature)
    {
        if (string.IsNullOrEmpty(signature) || secret.Length == 0)
        {
            return null;
        }

        var expected = Sign(secret, body);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant())))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var waybill = root.GetProperty("waybill").GetString();
            var status = root.GetProperty("status").GetString();
            var eventId = root.GetProperty("eventId").GetString();
            var at = root.GetProperty("at").GetDateTimeOffset();
            return string.IsNullOrWhiteSpace(waybill) || string.IsNullOrWhiteSpace(status) || string.IsNullOrWhiteSpace(eventId)
                ? null
                : new CourierPush(waybill.Trim().ToUpperInvariant(), new CourierStatus(status, eventId, at));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>The signature a courier puts on a push: lowercase hex of HMAC-SHA256 over the raw body.</summary>
    public static string Sign(string secret, string body) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
}

/// <summary>Builds the couriers from configuration (CPT-7): an unknown or disabled code has no gateway, and the booking order is primary first.</summary>
public sealed class CourierRegistry : ICourierRegistry
{
    private readonly List<ICourierGateway> _ordered;

    public CourierRegistry(IOptions<ShippingOptions> options, FakeCourierState state)
    {
        _ordered = [];
        foreach (var id in new[] { Couriers.Primary, Couriers.Secondary })
        {
            if (options.Value.Couriers.TryGetValue(Couriers.CodeOf(id), out var settings) && settings.Enabled)
            {
                _ordered.Add(new FakeCourierGateway(id, settings.WebhookSecret, settings.PushesStatus, state));
            }
        }
    }

    public IReadOnlyList<ICourierGateway> InOrder => _ordered;

    public ICourierGateway? Find(short courierId) => _ordered.FirstOrDefault(g => g.CourierId == courierId);

    public ICourierGateway? FindByCode(string? code) => Couriers.FromCode(code) is { } id ? Find(id) : null;
}
