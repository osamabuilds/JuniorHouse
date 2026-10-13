using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.PrintJobs;

namespace Romp.Modules.Labelling.Application.PrintJobs;

public sealed record PrintJobDto(
    long Id,
    string JobNo,
    string KindCode,
    string StatusCode,
    long VariantGtinId,
    long? WorkItemId,
    string SkuCode,
    string Gtin,
    int Qty,
    decimal PriceSnapshot,
    string PrinterCode,
    int AttemptCount,
    string? LastError,
    string? ReprintReasonCode,
    string? Note,
    string RequestedBy,
    string? ManualActionBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset NextAttemptAt);

public sealed record PrinterDto(short Id, string Code, string Name, string FormatCode);

public static class PrintCodes
{
    public static string Status(short id) => id switch
    {
        PrintJobStatus.Queued => "Queued",
        PrintJobStatus.Printing => "Printing",
        PrintJobStatus.Printed => "Printed",
        PrintJobStatus.Failed => "Failed",
        PrintJobStatus.Cancelled => "Cancelled",
        _ => "Unknown",
    };

    public static string ReprintReason(short? id) => id switch
    {
        ReprintReasons.Lost => "Lost",
        ReprintReasons.Damaged => "Damaged",
        ReprintReasons.PriceChange => "PriceChange",
        ReprintReasons.Other => "Other",
        _ => string.Empty,
    };

    public static short? StatusId(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "queued" => PrintJobStatus.Queued,
        "printing" => PrintJobStatus.Printing,
        "printed" => PrintJobStatus.Printed,
        "failed" => PrintJobStatus.Failed,
        "cancelled" => PrintJobStatus.Cancelled,
        _ => null,
    };
}

/// <summary>A job row as read from the database, joined to its variant and printer (one query, no per-row lookups).</summary>
internal sealed record PrintJobRecord(
    long Id,
    string JobNo,
    short KindId,
    short StatusId,
    long VariantGtinId,
    long? WorkItemId,
    string SkuCode,
    string Gtin,
    int Qty,
    decimal PriceSnapshot,
    string PrinterCode,
    int AttemptCount,
    string? LastError,
    short? ReprintReasonId,
    string? Note,
    string RequestedBy,
    string? ManualActionBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset NextAttemptAt)
{
    public PrintJobDto ToDto() => new(
        Id, JobNo, PrintJobKind.CodeOf(KindId), PrintCodes.Status(StatusId), VariantGtinId, WorkItemId, SkuCode, Gtin, Qty,
        PriceSnapshot, PrinterCode, AttemptCount, LastError, ReprintReasonId is null ? null : PrintCodes.ReprintReason(ReprintReasonId),
        Note, RequestedBy, ManualActionBy, CreatedAt, NextAttemptAt);
}

/// <summary>A job joined to its variant and printer, in a shape (settable members) that EF can filter and order before projecting.</summary>
internal sealed class PrintJobRow
{
    public PrintJob Job { get; init; } = null!;

    public string SkuCode { get; init; } = string.Empty;

    public string Gtin { get; init; } = string.Empty;

    public string PrinterCode { get; init; } = string.Empty;
}
