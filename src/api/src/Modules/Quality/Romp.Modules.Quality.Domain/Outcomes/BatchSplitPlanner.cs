using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Quality.Domain.Outcomes;

/// <summary>A quantity of one size and colour in a batch or in one part of a split.</summary>
public sealed record PartLine(short SizeId, short ColourId, int Qty);

/// <summary>One part of a batch decision: its outcome, the reason (mandatory except for Accepted) and the quantities it covers.</summary>
public sealed record DecisionPart(short OutcomeId, string? Reason, IReadOnlyList<PartLine> Lines);

/// <summary>
/// Pure rule for splitting a batch (spec AC-22): the parts' quantities must add up, size by size and colour by colour, to
/// exactly the batch's counted quantity: no unit lost, none counted twice, nothing on a line the batch does not have.
/// A whole-batch decision is simply one part that covers every line.
/// </summary>
public static class BatchSplitPlanner
{
    public static void Validate(IReadOnlyCollection<(short SizeId, short ColourId, int Qty)> batchLines, IReadOnlyList<DecisionPart> parts)
    {
        if (parts.Count == 0)
        {
            throw new DomainException("Decide at least one part of the batch.");
        }

        var batch = batchLines.ToDictionary(l => (l.SizeId, l.ColourId), l => l.Qty);
        var used = new Dictionary<(short, short), int>();

        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            if (part.Lines.Count == 0)
            {
                throw new DomainException($"Part {i + 1} has no quantities; give each part at least one size and colour.");
            }

            foreach (var line in part.Lines)
            {
                if (line.Qty < 1)
                {
                    throw new DomainException($"Part {i + 1}: each quantity must be a whole number above 0 (size {line.SizeId}, colour {line.ColourId}).");
                }

                if (!batch.ContainsKey((line.SizeId, line.ColourId)))
                {
                    throw new DomainException($"Part {i + 1}: size {line.SizeId} and colour {line.ColourId} is not part of this batch.");
                }

                used[(line.SizeId, line.ColourId)] = used.GetValueOrDefault((line.SizeId, line.ColourId)) + line.Qty;
            }

            if (part.Lines.GroupBy(l => (l.SizeId, l.ColourId)).Any(g => g.Count() > 1))
            {
                throw new DomainException($"Part {i + 1} lists the same size and colour more than once.");
            }
        }

        foreach (var (key, qty) in batch)
        {
            var assigned = used.GetValueOrDefault(key);
            if (assigned > qty)
            {
                throw new DomainException($"Size {key.SizeId} and colour {key.ColourId}: {assigned} assigned but the batch has only {qty}.");
            }

            if (assigned < qty)
            {
                throw new DomainException($"Size {key.SizeId} and colour {key.ColourId}: only {assigned} of {qty} assigned; every unit must be in a part.");
            }
        }
    }
}
