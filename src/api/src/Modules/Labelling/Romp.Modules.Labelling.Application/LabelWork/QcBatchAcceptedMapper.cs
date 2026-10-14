using Romp.Modules.Labelling.Domain;
using Romp.Modules.Quality.Contracts;

namespace Romp.Modules.Labelling.Application.LabelWork;

public sealed record MappedLine(short SizeId, short ColourId, int Qty);

public sealed record MappedBatch(long SubBatchId, long QcBatchId, long PoId, short OutcomeId, IReadOnlyList<MappedLine> Lines);

/// <summary>The result of mapping QC's event: a clean batch, or the reason it was refused (LW-6).</summary>
public sealed record MappingResult(MappedBatch? Batch, string? Rejection)
{
    public bool IsValid => Batch is not null;
}

/// <summary>
/// The anti-corruption layer (ADR 0011): the ONLY place that knows the shape of QC's <see cref="QcBatchAcceptedEvent"/>. If QC's
/// contract changes, this file changes and nothing else does. It also validates the message before anything is stored or any GTIN is
/// taken, so a forged or malformed event cannot consume numbers (LW-3, LW-6).
/// </summary>
public static class QcBatchAcceptedMapper
{
    public static MappingResult Map(QcBatchAcceptedEvent message, int maxLines)
    {
        if (message.SubBatchId <= 0 || message.BatchId <= 0 || message.PoId <= 0)
        {
            return Reject("The batch, sub-batch and PO ids must be positive.");
        }

        if (LabelOutcome.FromQcCode(message.OutcomeCode) is not { } outcome)
        {
            return Reject($"Outcome '{message.OutcomeCode}' is not a released outcome; only Accepted and Concession are labelled.");
        }

        if (message.Lines is null || message.Lines.Count == 0)
        {
            return Reject("The message has no lines.");
        }

        if (message.Lines.Count > maxLines)
        {
            return Reject($"The message has {message.Lines.Count} lines; the limit is {maxLines}.");
        }

        var seen = new HashSet<(short, short)>();
        var lines = new List<MappedLine>(message.Lines.Count);
        foreach (var line in message.Lines)
        {
            if (line.Qty <= 0 || line.SizeId <= 0 || line.ColourId <= 0)
            {
                return Reject("Every line needs a positive quantity, size and colour.");
            }

            if (!seen.Add((line.SizeId, line.ColourId)))
            {
                return Reject($"Size {line.SizeId} and colour {line.ColourId} appear twice in one sub-batch.");
            }

            lines.Add(new MappedLine(line.SizeId, line.ColourId, line.Qty));
        }

        return new MappingResult(new MappedBatch(message.SubBatchId, message.BatchId, message.PoId, outcome, lines), null);
    }

    private static MappingResult Reject(string reason) => new(null, reason);
}
