using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Application.Abstractions;
using Romp.Modules.Notifications.Domain.Channels;

namespace Romp.Modules.Notifications.Application.Channels;

/// <summary>Turns a phone number into the salted hash the STOP list stores, so the list holds no phone number (CHN-6, SEC-25).</summary>
public static class NumberHasher
{
    /// <summary>Digits only, in international form without the plus: <c>03331234567</c>, <c>+92 333 1234567</c> and <c>923331234567</c> are one number.</summary>
    public static string Normalise(string number)
    {
        ArgumentNullException.ThrowIfNull(number);
        var digits = new string(number.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 11 && digits[0] == '0' ? "92" + digits[1..] : digits;
    }

    public static string Hash(string? salt, string number) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(salt ?? string.Empty), Encoding.UTF8.GetBytes(Normalise(number))));
}

/// <summary>Asks whether a number has said STOP. Every WhatsApp plan and send asks (CHN-6).</summary>
public interface ISuppressionList
{
    Task<bool> IsBlockedAsync(string number, CancellationToken cancellationToken);
}

public sealed class SuppressionList(INotificationsDbContext dbContext, WhatsAppSettings settings) : ISuppressionList
{
    public async Task<bool> IsBlockedAsync(string number, CancellationToken cancellationToken)
    {
        if (NumberHasher.Normalise(number).Length == 0)
        {
            return false;
        }

        var hash = NumberHasher.Hash(settings.NumberSalt, number);
        return await dbContext.BlockedNumbers.AnyAsync(b => b.NumberHash == hash, cancellationToken);
    }
}

/// <summary>
/// STOP stops WhatsApp at once (CHN-6): the replying number joins the list as a salted hash. Saying STOP twice changes nothing. The customer can
/// still be reached by email.
/// </summary>
public sealed class StopHandler(INotificationsDbContext dbContext, WhatsAppSettings settings, TimeProvider clock) : IWhatsAppInboundHandler
{
    public WhatsAppEventType EventType => WhatsAppEventType.Stop;

    public async Task<long?> HandleAsync(WhatsAppInboundEvent inbound, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inbound);
        if (NumberHasher.Normalise(inbound.SenderNumber).Length == 0)
        {
            return null;
        }

        var hash = NumberHasher.Hash(settings.NumberSalt, inbound.SenderNumber);
        if (!await dbContext.BlockedNumbers.AnyAsync(b => b.NumberHash == hash, cancellationToken))
        {
            dbContext.BlockedNumbers.Add(new BlockedNumber(hash, clock.GetUtcNow()));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return null;
    }
}
