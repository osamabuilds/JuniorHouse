namespace Romp.Modules.Shipping.Domain.CodReconciliation;

/// <summary>What the judge needs to know about the parcel a statement line points at (null when the waybill is unknown or another courier's).</summary>
public sealed record CodParcelFacts(bool IsDelivered, decimal CashToCollect, short CodStatusId);

/// <summary>
/// Judges one statement line against the parcel (COD-2 to COD-7), as a pure function so every rule is a table row. The order of the checks matters:
/// unknown first, then not delivered, then not COD, then a parcel already paid, then the amounts.
/// </summary>
public static class CodJudge
{
    public static short Judge(CodParcelFacts? parcel, decimal remitted)
    {
        if (parcel is null)
        {
            return RemittanceOutcomes.Unknown;
        }

        if (!parcel.IsDelivered)
        {
            return RemittanceOutcomes.NotDelivered;
        }

        if (parcel.CashToCollect <= 0 || parcel.CodStatusId == CodStatuses.NotCod)
        {
            return RemittanceOutcomes.NotCod;
        }

        if (parcel.CodStatusId is CodStatuses.Settled or CodStatuses.Resolved or CodStatuses.ShortPaid or CodStatuses.Over)
        {
            return RemittanceOutcomes.Duplicate;
        }

        return remitted == parcel.CashToCollect ? RemittanceOutcomes.Matched : remitted < parcel.CashToCollect ? RemittanceOutcomes.ShortPaid : RemittanceOutcomes.Over;
    }
}
