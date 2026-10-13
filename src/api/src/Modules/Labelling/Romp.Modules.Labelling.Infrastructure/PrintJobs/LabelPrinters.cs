using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Storage;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Application.PrintJobs;
using Romp.Modules.Labelling.Application.Tags;
using Romp.Modules.Labelling.Domain;

namespace Romp.Modules.Labelling.Infrastructure.PrintJobs;

/// <summary>
/// Chooses the printer implementation by the printer's format (ADR 0010). Printer problems come back as failed outcomes, never as
/// exceptions, so one bad printer cannot break the worker's pass over the others (PJ-4).
/// </summary>
public sealed class LabelPrinterRouter(PdfPreviewLabelPrinter pdf, ZplLabelPrinter zpl) : ILabelPrinter
{
    public Task<PrintOutcome> SendAsync(PrinterTarget printer, LabelDocument document, CancellationToken cancellationToken) =>
        printer.FormatId == PrintFormat.Zpl
            ? zpl.SendAsync(printer, document, cancellationToken)
            : pdf.SendAsync(printer, document, cancellationToken);
}

/// <summary>
/// The development "printer" (D7): renders the tag PDF, stores it with the file storage under a generated key (never a name taken
/// from input), and reports success; the address "jam" always fails so the retry path can be seen. It is refused unless
/// <see cref="LabellingOptions.AllowFakePrinters"/> is on, so production can never report tags as printed that were not.
/// </summary>
public sealed class PdfPreviewLabelPrinter(IFileStorage storage, IOptions<LabellingOptions> options)
{
    public async Task<PrintOutcome> SendAsync(PrinterTarget printer, LabelDocument document, CancellationToken cancellationToken)
    {
        if (!options.Value.AllowFakePrinters)
        {
            return PrintOutcome.Fail("The preview printer is disabled in this environment; choose a real printer.");
        }

        if (string.Equals(printer.Address, "jam", StringComparison.OrdinalIgnoreCase))
        {
            return PrintOutcome.Fail("Paper jam (simulated).");
        }

        var key = await storage.SaveAsync(document.Content, cancellationToken);
        return PrintOutcome.Ok(key);
    }
}

/// <summary>
/// Sends ZPL to a network label printer (TCP, usually port 9100). The address comes only from the printer's configured row and must
/// match the configured host allow-list (PJ-12, SSRF defence in depth); connecting and sending are bounded by timeouts.
/// </summary>
public sealed class ZplLabelPrinter(IOptions<LabellingOptions> options)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

    public async Task<PrintOutcome> SendAsync(PrinterTarget printer, LabelDocument document, CancellationToken cancellationToken)
    {
        if (!TryParse(printer.Address, out var host, out var port))
        {
            return PrintOutcome.Fail($"Printer {printer.Code} has an invalid address.");
        }

        if (!options.Value.AllowedPrinterHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            return PrintOutcome.Fail($"Printer host {host} is not on the allowed list.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, timeout.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync(document.Content, timeout.Token);
            await stream.FlushAsync(timeout.Token);
            return PrintOutcome.Ok();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PrintOutcome.Fail("The printer did not answer in time.");
        }
        catch (Exception ex) when (ex is SocketException or IOException)
        {
            return PrintOutcome.Fail("The printer could not be reached or refused the job.");
        }
    }

    internal static bool TryParse(string address, out string host, out int port)
    {
        host = string.Empty;
        port = 0;
        var parts = address.Split(':');
        if (parts.Length != 2 || parts[0].Length == 0 || !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535)
        {
            return false;
        }

        host = parts[0];
        return true;
    }
}
