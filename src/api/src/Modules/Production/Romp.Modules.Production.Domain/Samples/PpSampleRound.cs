using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Production.Domain.Samples;

/// <summary>PROD.PP_SMPL_DCSN_LKP ids.</summary>
public static class PpDecision
{
    public const short Pending = 1;
    public const short Approved = 2;
    public const short Rejected = 3;
}

/// <summary>Maps to PROD.PP_SMPL_RND: one round of pre-production sampling (D6: no cap on rounds).</summary>
public sealed class PpSampleRound : Entity<long>, IAuditable
{
    private readonly List<PpSampleFile> _files = [];

    private PpSampleRound()
    {
        SubmittedBy = string.Empty;
    }

    internal PpSampleRound(long runId, int roundNo, DateOnly submittedDate, string submittedBy, string? note)
    {
        RunId = runId;
        RoundNo = roundNo;
        SubmittedDate = submittedDate;
        SubmittedBy = submittedBy;
        Note = note;
        DecisionId = PpDecision.Pending;
    }

    public long RunId { get; private set; }

    public int RoundNo { get; private set; }

    public DateOnly SubmittedDate { get; private set; }

    /// <summary>D13: the Romp person who recorded the submission.</summary>
    public string SubmittedBy { get; private set; }

    public string? Note { get; private set; }

    public short DecisionId { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>D13: the Romp person who approved or rejected.</summary>
    public string? DecidedBy { get; private set; }

    public string? DecisionReason { get; private set; }

    public IReadOnlyCollection<PpSampleFile> Files => _files.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public bool IsPending => DecisionId == PpDecision.Pending;

    internal void Decide(short decisionId, string decidedBy, string? reason, DateTimeOffset now)
    {
        DecisionId = decisionId;
        DecidedBy = decidedBy;
        DecisionReason = reason;
        DecidedAt = now;
    }

    public PpSampleFile AddFile(string storageKey, string fileName, string contentType, long sizeBytes)
    {
        var file = new PpSampleFile(storageKey, fileName, contentType, sizeBytes);
        _files.Add(file);
        return file;
    }
}

/// <summary>Maps to PROD.PP_SMPL_FILE (D7): a photo/file on a round. Keys are generated, never derived from the name.</summary>
public sealed class PpSampleFile : Entity<long>, IAuditable
{
    private PpSampleFile()
    {
        StorageKey = string.Empty;
        FileName = string.Empty;
        ContentType = string.Empty;
    }

    internal PpSampleFile(string storageKey, string fileName, string contentType, long sizeBytes)
    {
        StorageKey = storageKey;
        FileName = fileName;
        ContentType = contentType;
        FileSizeBytes = sizeBytes;
    }

    public long RoundId { get; private set; }

    public string StorageKey { get; private set; }

    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    public long FileSizeBytes { get; private set; }

    public bool IsDeleted { get; private set; }

    public void SoftDelete() => IsDeleted = true;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
