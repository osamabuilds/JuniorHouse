using Romp.BuildingBlocks.Domain;
using Romp.Modules.Vendor.Domain.PurchaseOrders;

namespace Romp.Modules.Vendor.Domain.Files;

/// <summary>
/// SCRUM-93 task 35/39 (AC-40, AC-41, AC-42): which file add/remove actions a PO's status allows.
/// Draft: anything. After Send: internal files can be added but never removed; vendor-visible files
/// are locked (they change only through an amendment). Cancelled: nothing changes.
/// </summary>
public static class PoFilePolicy
{
    public static void EnsureCanAdd(string poNo, short poStatusId, short categoryId)
    {
        EnsureNotCancelled(poNo, poStatusId, "have files added");

        if (poStatusId != PoStatus.Draft && PoFileCategory.IsVendorVisible(categoryId))
        {
            throw new DomainException(
                $"PO {poNo} has been sent, so vendor-visible files can no longer be added directly. Add the file through an amendment instead.");
        }
    }

    public static void EnsureCanRemove(string poNo, short poStatusId, short categoryId)
    {
        EnsureNotCancelled(poNo, poStatusId, "have files removed");

        if (poStatusId == PoStatus.Draft)
        {
            return;
        }

        throw new DomainException(PoFileCategory.IsVendorVisible(categoryId)
            ? $"PO {poNo} has been sent, so vendor-visible files can no longer be removed directly. Retire the file through an amendment instead."
            : $"PO {poNo} has been sent, so internal files can no longer be removed. They stay on record.");
    }

    private static void EnsureNotCancelled(string poNo, short poStatusId, string action)
    {
        if (poStatusId == PoStatus.Cancelled)
        {
            throw new DomainException($"PO {poNo} is cancelled and cannot {action}. Its existing files remain viewable.");
        }
    }
}
