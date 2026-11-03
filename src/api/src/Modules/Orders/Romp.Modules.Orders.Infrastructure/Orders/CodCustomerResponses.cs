using MediatR;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Orders.Infrastructure.Orders;

/// <summary>Notifications' way in for a customer's WhatsApp reply (ADR 0026): sends the command through the pipeline so the change is saved and audited like any other.</summary>
internal sealed class CodCustomerResponses(ISender sender) : ICodCustomerResponses
{
    public Task<CodReplyOutcome> RecordReplyAsync(long orderId, string senderMobile, bool confirm, CancellationToken cancellationToken) =>
        sender.Send(new CustomerRepliesOnWhatsAppCommand(orderId, senderMobile, confirm), cancellationToken);
}
