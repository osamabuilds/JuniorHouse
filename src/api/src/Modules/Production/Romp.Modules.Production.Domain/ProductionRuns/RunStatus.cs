namespace Romp.Modules.Production.Domain.ProductionRuns;

/// <summary>PROD.PROD_RUN_STS_LKP ids (system-owned, seeded by migration).</summary>
public static class RunStatus
{
    public const short Open = 1;
    public const short Cancelled = 2;
    public const short Closed = 3;

    public static string Code(short id) => id switch
    {
        Open => "Open",
        Cancelled => "Cancelled",
        Closed => "Closed",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown id."),
    };
}

/// <summary>REF.FBRC_RESP_LKP ids PROD depends on (a system-owned lookup; PROD cannot query REF's table, ADR 0002).</summary>
public static class FabricResponsibility
{
    public const short VendorSupplied = 1;
    public const short RompSupplied = 2;
}
