namespace Romp.Modules.Labelling.Domain.PrintJobs;

/// <summary>The yearly counter behind <c>PJ-{YYYY}-{NNNNN}</c> job numbers (ADR 0006). Maps to SKU.JOB_NO_SEQ; written by atomic SQL only.</summary>
public sealed class JobNumberSequence
{
    public short Year { get; private set; }

    public int Seq { get; private set; }
}
