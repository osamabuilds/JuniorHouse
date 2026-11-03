using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Notifications.Application.Abstractions;
using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Application.DeliveryLog;

/// <summary>
/// One row of the delivery log: one message on one channel (NFR-FT-07). The recipient is masked and no body is ever included (DLG-6, SEC-25);
/// both come from Orders' masked summary, not from this module's tables, which hold neither.
/// </summary>
public sealed record DeliveryLogRowDto(
    long Id,
    long NotificationId,
    string Kind,
    string Channel,
    string Status,
    int Attempts,
    string? LastError,
    long? OrderId,
    string? OrderNumber,
    string? Recipient,
    DateTimeOffset CreatedAt,
    DateTimeOffset? NextAttemptAt,
    bool IsResend,
    string? ResentBy,
    bool CanResend);

/// <summary>What the log screen can narrow by. Codes are the four-letter lookup codes (<c>FAIL</c>, <c>EMAL</c>, <c>ORCF</c>); a code that means nothing is ignored.</summary>
public sealed record DeliveryLogFilter(
    string? Status = null,
    string? Channel = null,
    string? Kind = null,
    bool DeadLettersOnly = false,
    long? OrderId = null,
    string? Search = null);

/// <summary>One attempt in a delivery's history (DLV-12): outcome and error category only.</summary>
public sealed record DeliveryAttemptDto(int AttemptNumber, string Outcome, string? Error, DateTimeOffset At);

/// <summary>A delivery with its attempts and, when it died, what became of the dead letter.</summary>
public sealed record DeliveryLogDetailDto(
    DeliveryLogRowDto Row,
    DateTimeOffset ExpiresAt,
    long? ResendOfId,
    IReadOnlyList<DeliveryAttemptDto> Attempts,
    DateTimeOffset? DeadAt,
    string? ResentBy,
    DateTimeOffset? ResentAt,
    bool WarnsOfDuplicate);

/// <summary>The strip above the log: queue age, what is waiting, what needs a person, and today's WhatsApp cost driver (DLV-11, CHN-15).</summary>
public sealed record DeliverySummaryDto(
    IReadOnlyDictionary<string, int> CountsByStatus,
    int QueuedCount,
    int DeadLettersAwaitingResend,
    int OldestDueSeconds,
    int WhatsAppSentToday);

/// <summary>Stable text for the enums, shared by the log and the resend.</summary>
internal static class DeliveryLogCodes
{
    public static TEnum? Parse<TEnum>(string? code, Func<TEnum, string> codeOf)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        foreach (var value in Enum.GetValues<TEnum>())
        {
            if (string.Equals(codeOf(value), code.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Resend is offered for anything that reached a final state except a deliberate skip; a queued message is already on its way.</summary>
    public static bool CanResend(DeliveryStatus status) =>
        status is DeliveryStatus.Sent or DeliveryStatus.Delivered or DeliveryStatus.Failed or DeliveryStatus.Dead or DeliveryStatus.Expired;

    /// <summary>A second copy would reach someone who probably has the first (DLG-7).</summary>
    public static bool WarnsOfDuplicate(DeliveryStatus status) => status is DeliveryStatus.Sent or DeliveryStatus.Delivered;
}

/// <summary>
/// The staff view of the delivery log (G1, NFR-FT-07). Paged, filtered in SQL, with the recipients fetched in one bulk call to Orders: a page
/// costs a fixed number of queries however many rows it holds (CONVENTIONS section 4).
/// </summary>
public sealed class DeliveryLogQueries(INotificationsDbContext dbContext, IOrderContactSummaries contacts, QueueHealth queueHealth, IDailySendCounter dailyCounter, TimeProvider clock)
{
    /// <summary>The most orders a search term can match; a mobile number or email rarely has more than a handful.</summary>
    private const int MaxSearchMatches = 200;

    private sealed class Row
    {
        public long Id { get; init; }

        public long NotificationId { get; init; }

        public NotificationKind Kind { get; init; }

        public NotificationChannel Channel { get; init; }

        public DeliveryStatus Status { get; init; }

        public int Attempts { get; init; }

        public ErrorCategory? LastError { get; init; }

        public string ReferenceKind { get; init; } = string.Empty;

        public long ReferenceId { get; init; }

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset? NextAttemptAt { get; init; }

        public long? ResendOfId { get; init; }

        public string CreatedBy { get; init; } = string.Empty;
    }

    public async Task<PagedResult<DeliveryLogRowDto>> ListAsync(DeliveryLogFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Joined();

        if (DeliveryLogCodes.Parse<DeliveryStatus>(filter.Status, NotificationCodes.Of) is { } status)
        {
            query = query.Where(r => r.Status == status);
        }

        if (DeliveryLogCodes.Parse<NotificationChannel>(filter.Channel, NotificationCodes.Of) is { } channel)
        {
            query = query.Where(r => r.Channel == channel);
        }

        if (DeliveryLogCodes.Parse<NotificationKind>(filter.Kind, NotificationCodes.Of) is { } kind)
        {
            query = query.Where(r => r.Kind == kind);
        }

        if (filter.DeadLettersOnly)
        {
            query = query.Where(r => r.Status == DeliveryStatus.Dead && !dbContext.DeadLetters.Any(d => d.DeliveryId == r.Id && d.ResentAt != null));
        }

        if (filter.OrderId is { } orderId)
        {
            query = query.Where(r => r.ReferenceKind == ReferenceKinds.Order && r.ReferenceId == orderId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var ids = await contacts.FindOrderIdsAsync(filter.Search, MaxSearchMatches, cancellationToken);
            if (ids.Count == 0)
            {
                var empty = page.Normalized();
                return new PagedResult<DeliveryLogRowDto>([], empty.Page, empty.PageSize, 0);
            }

            var idArray = ids.ToArray();
            query = query.Where(r => r.ReferenceKind == ReferenceKinds.Order && idArray.Contains(r.ReferenceId));
        }

        // Newest first; the delivery id is unique, so pages never overlap or skip.
        var ordered = query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id);
        var result = await ordered.ToPagedResultAsync(page, cancellationToken);

        var summaries = await contacts.FindManyAsync(OrderIdsOf(result.Items), cancellationToken);
        var rows = result.Items.Select(r => ToDto(r, summaries)).ToList();
        return new PagedResult<DeliveryLogRowDto>(rows, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<DeliveryLogDetailDto?> GetAsync(long deliveryId, CancellationToken cancellationToken)
    {
        var row = await Joined().Where(r => r.Id == deliveryId).SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var expiresAt = await dbContext.Notifications.AsNoTracking()
            .Where(n => n.Id == row.NotificationId).Select(n => n.ExpiresAt).SingleAsync(cancellationToken);

        var attempts = await dbContext.Attempts.AsNoTracking()
            .Where(a => a.DeliveryId == deliveryId)
            .OrderBy(a => a.AttemptNumber).ThenBy(a => a.Id)
            .Select(a => new { a.AttemptNumber, a.Outcome, a.Error, a.AttemptedAt })
            .ToListAsync(cancellationToken);

        var dead = await dbContext.DeadLetters.AsNoTracking()
            .Where(d => d.DeliveryId == deliveryId)
            .Select(d => new { d.DeadAt, d.ResentBy, d.ResentAt })
            .SingleOrDefaultAsync(cancellationToken);

        var summaries = await contacts.FindManyAsync(OrderIdsOf([row]), cancellationToken);
        return new DeliveryLogDetailDto(
            ToDto(row, summaries),
            expiresAt,
            row.ResendOfId,
            attempts.Select(a => new DeliveryAttemptDto(a.AttemptNumber, NotificationCodes.Of(a.Outcome), a.Error is { } e ? NotificationCodes.Of(e) : null, a.AttemptedAt)).ToList(),
            dead?.DeadAt,
            dead?.ResentBy,
            dead?.ResentAt,
            DeliveryLogCodes.WarnsOfDuplicate(row.Status));
    }

    public async Task<DeliverySummaryDto> SummaryAsync(CancellationToken cancellationToken)
    {
        var counts = await dbContext.Deliveries.AsNoTracking()
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var awaiting = await dbContext.Deliveries.AsNoTracking()
            .CountAsync(d => d.Status == DeliveryStatus.Dead && !dbContext.DeadLetters.Any(x => x.DeliveryId == d.Id && x.ResentAt != null), cancellationToken);

        var queue = await queueHealth.ReadAsync(cancellationToken);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var whatsApp = await dailyCounter.CountAsync(today, NotificationChannel.WhatsApp, cancellationToken);

        var byStatus = Enum.GetValues<DeliveryStatus>()
            .ToDictionary(s => NotificationCodes.Of(s), s => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0);
        return new DeliverySummaryDto(byStatus, queue.QueuedCount, awaiting, (int)queue.OldestDueAge.TotalSeconds, whatsApp);
    }

    private IQueryable<Row> Joined() =>
        from d in dbContext.Deliveries.AsNoTracking()
        join n in dbContext.Notifications.AsNoTracking() on d.NotificationId equals n.Id
        select new Row
        {
            Id = d.Id,
            NotificationId = n.Id,
            Kind = n.Kind,
            Channel = d.Channel,
            Status = d.Status,
            Attempts = d.AttemptCount,
            LastError = d.LastError,
            ReferenceKind = n.ReferenceKind,
            ReferenceId = n.ReferenceId,
            CreatedAt = d.InsrDte,
            NextAttemptAt = d.NextAttemptAt,
            ResendOfId = d.FallbackOfId,
            CreatedBy = d.InsrBy,
        };

    private static List<long> OrderIdsOf(IEnumerable<Row> rows) =>
        rows.Where(r => r.ReferenceKind == ReferenceKinds.Order).Select(r => r.ReferenceId).Distinct().ToList();

    private static DeliveryLogRowDto ToDto(Row r, IReadOnlyDictionary<long, OrderContactSummary> summaries)
    {
        summaries.TryGetValue(r.ReferenceId, out var order);
        var known = r.ReferenceKind == ReferenceKinds.Order && order is not null;
        var recipient = !known ? null : r.Channel == NotificationChannel.Email ? order!.MaskedEmail : order!.MaskedMobile;
        return new DeliveryLogRowDto(
            r.Id,
            r.NotificationId,
            NotificationCodes.Of(r.Kind),
            NotificationCodes.Of(r.Channel),
            NotificationCodes.Of(r.Status),
            r.Attempts,
            r.LastError is { } e ? NotificationCodes.Of(e) : null,
            r.ReferenceKind == ReferenceKinds.Order ? r.ReferenceId : null,
            known ? order!.OrderNumber : null,
            recipient,
            r.CreatedAt,
            r.NextAttemptAt,
            r.ResendOfId is not null,
            r.ResendOfId is not null && r.CreatedBy.Length > 0 ? r.CreatedBy : null,
            DeliveryLogCodes.CanResend(r.Status));
    }
}
