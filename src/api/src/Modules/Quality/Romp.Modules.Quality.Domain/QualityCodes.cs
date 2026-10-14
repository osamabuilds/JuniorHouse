namespace Romp.Modules.Quality.Domain;

// Stable lookup ids (plan.md "Lookups"); the codes are what events and screens carry, the ids are what tables store.

public static class GrnStatus
{
    public const short Recorded = 1;
}

public static class GrnReconciliation
{
    public const short Matched = 1;
    public const short Shortage = 2;
    public const short Over = 3;

    public static string Code(short id) => id switch
    {
        Matched => "Matched",
        Shortage => "Short",
        Over => "Over",
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };
}

public static class BatchStatus
{
    public const short Pending = 1;
    public const short InInspection = 2;
    public const short Decided = 3;
    public const short AwaitingRework = 4;
    public const short Closed = 5;
}

public static class QcOutcome
{
    public const short Accepted = 1;
    public const short Concession = 2;
    public const short Rejected = 3;
    public const short Rework = 4;

    public static string Code(short id) => id switch
    {
        Accepted => "Accepted",
        Concession => "Concession",
        Rejected => "Rejected",
        Rework => "Rework",
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };
}

public static class InspectionMode
{
    public const short Aql = 1;
    public const short Full = 2;

    public static string Code(short id) => id == Aql ? "Aql" : "Full";
}

public static class InspectionResult
{
    public const short Pass = 1;
    public const short Fail = 2;

    public static string Code(short id) => id == Pass ? "Pass" : "Fail";
}

public static class InspectionLevel
{
    public const short S1 = 1;
    public const short S2 = 2;
    public const short S3 = 3;
    public const short S4 = 4;
    public const short I = 5;
    public const short II = 6;
    public const short III = 7;
}

public static class AqlLevel
{
    public const short Aql065 = 1;
    public const short Aql100 = 2;
    public const short Aql150 = 3;
    public const short Aql250 = 4;
    public const short Aql400 = 5;
    public const short Aql650 = 6;
}

public static class CheckType
{
    public const short Stitching = 1;
    public const short Measurements = 2;
    public const short FabricColourMatch = 3;
    public const short TrimsSecure = 4;
    public const short VisibleDefects = 5;
    public const short CareLabel = 6;

    public static readonly short[] All = [Stitching, Measurements, FabricColourMatch, TrimsSecure, VisibleDefects, CareLabel];
}

public static class CheckResult
{
    public const short Pass = 1;
    public const short Fail = 2;
    public const short NotApplicable = 3;
}

public static class SafetyType
{
    public const short ChokingHazardTrims = 1;
    public const short DrawstringNeckline = 2;

    public static readonly short[] All = [ChokingHazardTrims, DrawstringNeckline];
}

public static class DefectType
{
    public const short Stitching = 1;
    public const short Measurement = 2;
    public const short FabricColour = 3;
    public const short TrimsSecurity = 4;
    public const short VisibleDefect = 5;
    public const short CareLabel = 6;
    public const short Other = 7;

    /// <summary>The defect type that a failed checklist item of the given <see cref="CheckType"/> requires (AC-17).</summary>
    public static short ForCheck(short checkTypeId) => checkTypeId switch
    {
        CheckType.Stitching => Stitching,
        CheckType.Measurements => Measurement,
        CheckType.FabricColourMatch => FabricColour,
        CheckType.TrimsSecure => TrimsSecurity,
        CheckType.VisibleDefects => VisibleDefect,
        CheckType.CareLabel => CareLabel,
        _ => throw new ArgumentOutOfRangeException(nameof(checkTypeId)),
    };
}

public static class DefectSeverity
{
    public const short Critical = 1;
    public const short Major = 2;
    public const short Minor = 3;
}
