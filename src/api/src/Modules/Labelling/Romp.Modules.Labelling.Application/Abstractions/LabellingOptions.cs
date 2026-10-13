namespace Romp.Modules.Labelling.Application.Abstractions;

/// <summary>
/// Configuration, not constants (D10, ADR 0009, ADR 0010). Bound from <c>Labelling:*</c> and validated at start-up by the module.
/// </summary>
public sealed class LabellingOptions
{
    /// <summary>The GS1 company prefix. Defaults to the restricted-circulation test range (D2), which production refuses.</summary>
    public string CompanyPrefix { get; set; } = "0200000";

    /// <summary>Extra tags allowed over the accepted quantity, in percent (D10). Default 0.</summary>
    public decimal SparePercent { get; set; }

    public int MaxTagsPerJob { get; set; } = 5_000;

    public int MaxQueuedJobsPerPrinter { get; set; } = 200;

    public int MaxLinesPerAcceptedBatch { get; set; } = 500;

    /// <summary>Attempts before a job is marked Failed (PJ-2).</summary>
    public int MaxAttempts { get; set; } = 4;

    /// <summary>Delay before attempt 2, 3, 4…; the last value repeats. Empty means the default schedule (30 s, 2 min, 10 min).</summary>
    public int[] BackoffSeconds { get; set; } = [];

    /// <summary>A job still Printing after this long is returned to Queued (PJ-9).</summary>
    public int StuckJobMinutes { get; set; } = 5;

    public string DefaultPrinterCode { get; set; } = "FAKE-PDF";

    /// <summary>Whether the PDF/fake printers may be used. True only in Development and Testing; never in production.</summary>
    public bool AllowFakePrinters { get; set; }

    /// <summary>The only hosts a ZPL printer address may point at (PJ-12, SSRF defence in depth).</summary>
    public string[] AllowedPrinterHosts { get; set; } = [];

    public int WorkerPollSeconds { get; set; } = 2;

    public Domain.Variants.CompanyPrefix Prefix => Domain.Variants.CompanyPrefix.Parse(CompanyPrefix);

    /// <summary>Effective backoff schedule: the configured seconds, or 30 s, 2 min, 10 min when none are configured.</summary>
    private static readonly int[] DefaultBackoffSeconds = [30, 120, 600];

    public IReadOnlyList<TimeSpan> Backoff =>
        (BackoffSeconds.Length == 0 ? DefaultBackoffSeconds : BackoffSeconds).Select(s => TimeSpan.FromSeconds(s)).ToList();
}
