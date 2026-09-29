namespace Romp.Modules.Quality.Domain.Inspections.Aql;

/// <summary>Maps to QC.QC_AQL_SMPL: the sample-size code letter and sample size for a lot-size range at one inspection level (ISO 2859-1 Tables I and 2-A). Seeded reference data.</summary>
public sealed class AqlSampleRow
{
    private AqlSampleRow()
    {
        CodeLetter = string.Empty;
    }

    public AqlSampleRow(short id, short inspectionLevelId, int lotMinQty, int lotMaxQty, string codeLetter, int sampleQty)
    {
        Id = id;
        InspectionLevelId = inspectionLevelId;
        LotMinQty = lotMinQty;
        LotMaxQty = lotMaxQty;
        CodeLetter = codeLetter;
        SampleQty = sampleQty;
    }

    public short Id { get; private set; }

    public short InspectionLevelId { get; private set; }

    public int LotMinQty { get; private set; }

    public int LotMaxQty { get; private set; }

    public string CodeLetter { get; private set; }

    public int SampleQty { get; private set; }
}

/// <summary>Maps to QC.QC_AQL_LIMT: accept and reject numbers for a code letter at one acceptable quality level. Seeded reference data.</summary>
public sealed class AqlLimitRow
{
    private AqlLimitRow()
    {
        CodeLetter = string.Empty;
    }

    public AqlLimitRow(short id, string codeLetter, short aqlLevelId, int acceptNo, int rejectNo)
    {
        Id = id;
        CodeLetter = codeLetter;
        AqlLevelId = aqlLevelId;
        AcceptNo = acceptNo;
        RejectNo = rejectNo;
    }

    public short Id { get; private set; }

    public string CodeLetter { get; private set; }

    public short AqlLevelId { get; private set; }

    public int AcceptNo { get; private set; }

    public int RejectNo { get; private set; }
}
