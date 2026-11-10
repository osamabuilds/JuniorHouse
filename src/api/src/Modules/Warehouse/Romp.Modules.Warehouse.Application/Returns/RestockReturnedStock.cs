using System.Security.Cryptography;
using System.Text;
using MediatR;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Application.Adjustments;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Adjustments;

namespace Romp.Modules.Warehouse.Application.Returns;

/// <summary>
/// Puts returned units back as a found-stock adjustment (ADR 0033, RQC-4, 9, 10), so every unit has an adjustment and a ledger row. The
/// caller's key becomes the adjustment's idempotency key, so a repeat finds the first posting and adds nothing.
/// </summary>
public sealed record RestockCommand(RestockRequest Request) : IRequest<RestockResult>, IWarehouseCommand;

public sealed class RestockCommandHandler(IAdjustmentPoster poster) : IRequestHandler<RestockCommand, RestockResult>
{
    public async Task<RestockResult> Handle(RestockCommand command, CancellationToken cancellationToken)
    {
        var r = command.Request;
        if (r.Quantity < 1 || string.IsNullOrWhiteSpace(r.IdempotencyKey) || string.IsNullOrWhiteSpace(r.BinCode) || string.IsNullOrWhiteSpace(r.ActingPerson))
        {
            return new RestockResult(false, "A restock needs a quantity of at least 1, a bin, a key and the person doing it.");
        }

        var key = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"restock:{r.IdempotencyKey}"))[..16]);
        try
        {
            // The poster checks everything before it saves, so a refusal leaves nothing behind. A return is bounded by the order, so the
            // large-adjustment re-confirmation is already given by the inspection.
            await poster.PostAsync(
                new AdjustmentRequest(
                    r.VariantId, null, r.BinCode, r.Seconds ? Grades.Concession : Grades.Accepted, r.Quantity, AdjustmentReasons.FoundStock,
                    "Returned stock, passed reverse QC", true, key, null, r.ActingPerson),
                cancellationToken);
            return new RestockResult(true, null);
        }
        catch (DomainException refusal)
        {
            return new RestockResult(false, refusal.Message);
        }
    }
}
