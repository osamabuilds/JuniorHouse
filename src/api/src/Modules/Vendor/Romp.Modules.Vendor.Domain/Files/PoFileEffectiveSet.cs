using Romp.Modules.Vendor.Domain.Revisions;

namespace Romp.Modules.Vendor.Domain.Files;

/// <summary>
/// SCRUM-93 task 37 (AC-43, AC-44): the vendor-visible files effective at a given revision. A file
/// is effective at revision N when it was added at or before N (a Draft-stage file, added in no
/// revision, counts from Rev 0) and not retired at or before N. Additions or retirements made by a
/// Rejected or Withdrawn revision never took effect, so they are ignored.
/// </summary>
public static class PoFileEffectiveSet
{
    public static IReadOnlyList<PurchaseOrderFile> At(
        IEnumerable<PurchaseOrderFile> files,
        IEnumerable<PurchaseOrderRevision> revisions,
        short revisionNumber)
    {
        var revisionById = revisions.ToDictionary(r => r.Id);

        bool Counts(long? revisionId, out short number)
        {
            number = 0;
            if (revisionId is null || !revisionById.TryGetValue(revisionId.Value, out var revision))
            {
                return false;
            }

            number = revision.RevisionNumber;
            return revision.StatusId is not (RevisionStatus.Rejected or RevisionStatus.Withdrawn);
        }

        return files
            .Where(f => !f.IsDeleted && PoFileCategory.IsVendorVisible(f.CategoryId))
            .Where(f => f.AddedInRevisionId is null || (Counts(f.AddedInRevisionId, out var added) && added <= revisionNumber))
            .Where(f => f.RetiredInRevisionId is null || !(Counts(f.RetiredInRevisionId, out var retired) && retired <= revisionNumber))
            .ToList();
    }
}
