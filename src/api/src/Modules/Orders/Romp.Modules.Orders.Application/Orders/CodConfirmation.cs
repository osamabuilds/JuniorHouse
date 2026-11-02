using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Domain.Orders;

namespace Romp.Modules.Orders.Application.Orders;

/// <summary>Makes and reads the signed links for cash on delivery confirmation (the page button and the email, COD-12). The key is a secret, never in the repository.</summary>
public interface ICodLinks
{
    /// <summary>A token for this order that works for <c>Orders:CodLinkDays</c>. It identifies one order and nothing else.</summary>
    string Create(string orderNumber, DateTimeOffset now);

    /// <summary>The order number inside a valid, unexpired token, or null for anything else.</summary>
    string? TryRead(string? token, DateTimeOffset now);
}

/// <summary>
/// Cancel an order that has not been dispatched (ORD-9, ORD-10). Used by staff and by the customer. A dispatched order is refused with a
/// message that points to returns. Cancelling twice changes nothing. The cancellation event makes Warehouse and Cart give back what they held.
/// </summary>
public sealed record CancelOrderCommand(string OrderNumber, short ReasonId, short ActorKindId, string? ActorRef, string? Note) : IRequest<CancelOrderResult>, IOrdersCommand;

public enum CancelOrderOutcome
{
    Cancelled,
    AlreadyCancelled,
    NotFound,
    TooLate,
}

public sealed record CancelOrderResult(CancelOrderOutcome Outcome, string? Message);

public sealed class CancelOrderCommandHandler(IOrdersDbContext dbContext, TimeProvider clock) : IRequestHandler<CancelOrderCommand, CancelOrderResult>
{
    public async Task<CancelOrderResult> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.Include(o => o.History).FirstOrDefaultAsync(o => o.OrderNumber == request.OrderNumber, cancellationToken);
        if (order is null)
        {
            return new CancelOrderResult(CancelOrderOutcome.NotFound, null);
        }

        switch (order.Cancel(request.ReasonId, request.ActorKindId, request.ActorRef, request.Note, wasPicked: false, clock.GetUtcNow()))
        {
            case OrderTransition.Changed:
                return new CancelOrderResult(CancelOrderOutcome.Cancelled, null);
            case OrderTransition.Unchanged:
                return new CancelOrderResult(CancelOrderOutcome.AlreadyCancelled, null);
            default:
                return new CancelOrderResult(CancelOrderOutcome.TooLate, Order.CannotCancelAfterDispatch);
        }
    }
}

/// <summary>
/// Staff record how a confirmation call ended (COD-3, COD-4, COD-8): confirmed, no answer, or refused. Audited by the Orders audit trail.
/// A refusal counts against the mobile number (COD-4); reaching the threshold only flags it (COD-5).
/// </summary>
public sealed record RecordCodOutcomeCommand(string OrderNumber, short OutcomeId, string StaffRef, string? Note) : IRequest<CodOutcomeResult>, IOrdersCommand;

public enum CodOutcomeKind
{
    Recorded,
    NotFound,
    NotPending,
    NotAllowedOutcome,
}

public sealed record CodOutcomeResult(CodOutcomeKind Kind);

public sealed class RecordCodOutcomeCommandHandler(IOrdersDbContext dbContext, TimeProvider clock, IOptions<OrdersOptions> options)
    : IRequestHandler<RecordCodOutcomeCommand, CodOutcomeResult>
{
    public async Task<CodOutcomeResult> Handle(RecordCodOutcomeCommand request, CancellationToken cancellationToken)
    {
        // Staff record the three call outcomes; the customer's own answers come through the signed link or WhatsApp.
        if (request.OutcomeId is not (CodOutcomes.Confirmed or CodOutcomes.NoAnswer or CodOutcomes.Refused))
        {
            return new CodOutcomeResult(CodOutcomeKind.NotAllowedOutcome);
        }

        var order = await dbContext.Orders.Include(o => o.History).Include(o => o.CodAttempts).FirstOrDefaultAsync(o => o.OrderNumber == request.OrderNumber, cancellationToken);
        if (order is null)
        {
            return new CodOutcomeResult(CodOutcomeKind.NotFound);
        }

        var result = order.RecordCodOutcome(request.OutcomeId, ActorKinds.Staff, request.StaffRef, request.Note, clock.GetUtcNow(), options.Value.CodPolicy);
        if (result == OrderTransition.NotAllowed)
        {
            return new CodOutcomeResult(CodOutcomeKind.NotPending);
        }

        if (request.OutcomeId == CodOutcomes.Refused)
        {
            await CodRiskBook.NoteRefusalAsync(dbContext, order.ContactMobile, options.Value.RefusalFlagThreshold, cancellationToken);
        }

        return new CodOutcomeResult(CodOutcomeKind.Recorded);
    }
}

/// <summary>Keeps the per-mobile refusal counts (COD-4, COD-5, COD-10).</summary>
public static class CodRiskBook
{
    public static async Task NoteRefusalAsync(IOrdersDbContext dbContext, string mobile, int threshold, CancellationToken cancellationToken)
    {
        var risk = await dbContext.CodRisks.FirstOrDefaultAsync(r => r.Mobile == mobile, cancellationToken);
        if (risk is null)
        {
            risk = new CodRisk(mobile);
            dbContext.CodRisks.Add(risk);
        }

        risk.NoteRefusal(threshold);
    }

    public static async Task NoteNoAnswerCancelAsync(IOrdersDbContext dbContext, string mobile, CancellationToken cancellationToken)
    {
        var risk = await dbContext.CodRisks.FirstOrDefaultAsync(r => r.Mobile == mobile, cancellationToken);
        if (risk is null)
        {
            risk = new CodRisk(mobile);
            dbContext.CodRisks.Add(risk);
        }

        risk.NoteNoAnswerCancel();
    }
}

/// <summary>
/// The customer confirms or cancels their cash on delivery order from the signed link (page button or email, COD-12, COD-15). The link opens
/// a page and only this command, sent by a button press, changes anything (a mail scanner opening the link changes nothing). A used,
/// expired or finished link says "already handled" and changes nothing (COD-19). Cancelling is not a refusal (COD-15).
/// </summary>
public sealed record CustomerRespondsToCodCommand(string? Token, bool Confirm) : IRequest<CustomerCodResult>, IOrdersCommand;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum CustomerCodOutcome
{
    /// <summary>The link is not one of ours, has expired or is for no order.</summary>
    InvalidLink,

    Confirmed,
    Cancelled,

    /// <summary>The order was already confirmed, cancelled or has moved on: nothing changed.</summary>
    AlreadyHandled,
}

public sealed record CustomerCodResult(CustomerCodOutcome Outcome, string? OrderNumber);

public sealed class CustomerRespondsToCodCommandHandler(IOrdersDbContext dbContext, ICodLinks links, TimeProvider clock, IOptions<OrdersOptions> options)
    : IRequestHandler<CustomerRespondsToCodCommand, CustomerCodResult>
{
    public async Task<CustomerCodResult> Handle(CustomerRespondsToCodCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var number = links.TryRead(request.Token, now);
        if (number is null)
        {
            return new CustomerCodResult(CustomerCodOutcome.InvalidLink, null);
        }

        var order = await dbContext.Orders.Include(o => o.History).Include(o => o.CodAttempts).FirstOrDefaultAsync(o => o.OrderNumber == number, cancellationToken);
        if (order is null)
        {
            return new CustomerCodResult(CustomerCodOutcome.InvalidLink, null);
        }

        var outcome = request.Confirm ? CodOutcomes.ConfirmedByCustomerOnPage : CodOutcomes.CancelledByCustomerOnPage;
        return order.RecordCodOutcome(outcome, ActorKinds.Customer, null, null, now, options.Value.CodPolicy) == OrderTransition.Changed
            ? new CustomerCodResult(request.Confirm ? CustomerCodOutcome.Confirmed : CustomerCodOutcome.Cancelled, number)
            : new CustomerCodResult(CustomerCodOutcome.AlreadyHandled, number);
    }
}

/// <summary>
/// The background pass over cash on delivery orders (COD-2, COD-13): cancel those whose final wait passed without a confirmation, and raise
/// the WhatsApp confirmation request, once, for opted-in orders still unconfirmed after the wait. Every step is idempotent by state, and a
/// second instance changing the same order loses on the row version and simply tries again next round, so none is processed twice.
/// </summary>
public sealed record RunCodConfirmationCommand(int BatchSize = RunCodConfirmationCommand.DefaultBatchSize) : IRequest<CodRunResult>, IOrdersCommand
{
    public const int DefaultBatchSize = 100;
}

public sealed record CodRunResult(int AutoCancelled, int WhatsAppRequested);

public sealed class RunCodConfirmationCommandHandler(IOrdersDbContext dbContext, TimeProvider clock, IOptions<OrdersOptions> options)
    : IRequestHandler<RunCodConfirmationCommand, CodRunResult>
{
    public async Task<CodRunResult> Handle(RunCodConfirmationCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var policy = options.Value.CodPolicy;
        var take = Math.Clamp(request.BatchSize, 1, 500);

        var overdue = await dbContext.Orders.Include(o => o.History)
            .Where(o => o.StatusId == OrderStatuses.PendingConfirmation && o.CodAutoCancelAt != null && o.CodAutoCancelAt <= now)
            .OrderBy(o => o.CodAutoCancelAt).Take(take).ToListAsync(cancellationToken);
        var cancelled = 0;
        foreach (var order in overdue)
        {
            if (order.AutoCancelIfDue(now) == OrderTransition.Changed)
            {
                await CodRiskBook.NoteNoAnswerCancelAsync(dbContext, order.ContactMobile, cancellationToken);
                cancelled++;
            }
        }

        var askAfter = now - policy.WhatsAppConfirmAfter;
        var candidates = await dbContext.Orders
            .Where(o => o.StatusId == OrderStatuses.PendingConfirmation && o.PaymentMethodId == PaymentMethods.CashOnDelivery
                && o.WhatsAppOptIn && o.CodWhatsAppAskedAt == null && o.PlacedAt <= askAfter)
            .OrderBy(o => o.PlacedAt).Take(take).ToListAsync(cancellationToken);
        var asked = candidates.Count(o => o.RequestWhatsAppConfirmation(policy, now));
        return new CodRunResult(cancelled, asked);
    }
}

/// <summary>What the confirm page needs to show when the link opens, without changing anything (COD-12, COD-19).</summary>
public sealed record GetCodLinkStatusQuery(string? Token) : IRequest<CodLinkStatus>;

public sealed record CodLinkStatus(bool IsValid, string? OrderNumber, bool CanRespond, string? StatusCode);

public sealed class GetCodLinkStatusQueryHandler(IOrdersDbContext dbContext, ICodLinks links, TimeProvider clock) : IRequestHandler<GetCodLinkStatusQuery, CodLinkStatus>
{
    public async Task<CodLinkStatus> Handle(GetCodLinkStatusQuery request, CancellationToken cancellationToken)
    {
        var number = links.TryRead(request.Token, clock.GetUtcNow());
        var found = number is null
            ? null
            : await dbContext.Orders.AsNoTracking().Where(o => o.OrderNumber == number).Select(o => new { o.StatusId, o.PaymentMethodId }).FirstOrDefaultAsync(cancellationToken);
        return found is null || number is null
            ? new CodLinkStatus(false, null, false, null)
            : new CodLinkStatus(true, number, found.StatusId == OrderStatuses.PendingConfirmation && PaymentMethods.IsCashOnDelivery(found.PaymentMethodId), OrderStatuses.CodeOf(found.StatusId));
    }
}
