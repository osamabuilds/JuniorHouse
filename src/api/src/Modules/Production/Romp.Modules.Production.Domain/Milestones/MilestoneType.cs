namespace Romp.Modules.Production.Domain.Milestones;

/// <summary>PROD.MLST_TYP_LKP ids (D10). System-owned, seeded by migration.</summary>
public static class MilestoneType
{
    public const short FabricBooked = 1;
    public const short FabricDispatchedByRomp = 2;
    public const short PpSampleSubmitted = 3;
    public const short PpSampleApproved = 4;
    public const short PpSampleRejected = 5;
    public const short BulkCuttingStarted = 6;
    public const short FinishedReadyToShip = 7;

    public static string Code(short id) => id switch
    {
        FabricBooked => "FabricBooked",
        FabricDispatchedByRomp => "FabricDispatchedByRomp",
        PpSampleSubmitted => "PPSampleSubmitted",
        PpSampleApproved => "PPSampleApproved",
        PpSampleRejected => "PPSampleRejected",
        BulkCuttingStarted => "BulkCuttingStarted",
        FinishedReadyToShip => "FinishedReadyToShip",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown id."),
    };

    /// <summary>Types staff record directly; the three PP types come from the sample-round actions instead.</summary>
    public static bool IsGeneric(short id) => id is FabricBooked or FabricDispatchedByRomp or BulkCuttingStarted or FinishedReadyToShip;
}
