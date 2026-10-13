using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Storage;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Application.PrintJobs;
using Romp.Modules.Labelling.Application.Tags;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Infrastructure.PrintJobs;

namespace Romp.Modules.Labelling.Tests.PrintJobs;

/// <summary>ADR 0010 / ADR 0011: the only outbound connection Labelling makes is to a configured, allow-listed printer, with timeouts.</summary>
public sealed class LabelPrinterTests
{
    private sealed class MemoryStorage : IFileStorage
    {
        public List<byte[]> Saved { get; } = [];

        public Task<string> SaveAsync(byte[] content, CancellationToken cancellationToken)
        {
            Saved.Add(content);
            return Task.FromResult($"key-{Saved.Count}");
        }

        public Task<byte[]> RetrieveAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult(Saved[0]);
    }

    private static readonly LabelDocument Zpl = new(PrintFormat.Zpl, Encoding.UTF8.GetBytes("^XA^FDhello^FS^XZ"), "application/x-zpl", "ROMP-X", "5901234123457");
    private static readonly LabelDocument Pdf = new(PrintFormat.Pdf, Encoding.ASCII.GetBytes("%PDF-1.4"), "application/pdf", "ROMP-X", "5901234123457");

    private static ZplLabelPrinter ZplPrinter(params string[] allowedHosts) =>
        new(Options.Create(new LabellingOptions { AllowedPrinterHosts = allowedHosts }));

    private static PrinterTarget Target(string address, short format = PrintFormat.Zpl) => new(9, "TEST", format, address);

    [Fact]
    [Trait("Spec", "PJ-12")]
    public async Task AHostThatIsNotOnTheAllowList_IsNeverContacted()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var outcome = await ZplPrinter("printer.lan").SendAsync(Target($"127.0.0.1:{port}"), Zpl, default);

        Assert.False(outcome.Success);
        Assert.Contains("allowed", outcome.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(listener.Pending(), "no connection may be attempted to a host that is not allow-listed");
    }

    [Theory]
    [InlineData("")]
    [InlineData("nohostport")]
    [InlineData(":9100")]
    [InlineData("127.0.0.1:")]
    [InlineData("127.0.0.1:0")]
    [InlineData("127.0.0.1:99999")]
    [InlineData("127.0.0.1:-1")]
    [InlineData("127.0.0.1:91x0")]
    [InlineData("a:b:c")]
    [InlineData("http://127.0.0.1:9100")]
    [Trait("Spec", "PJ-12")]
    public async Task AMalformedAddress_IsRefusedWithoutAnyNetworkCall(string address)
    {
        var outcome = await ZplPrinter("127.0.0.1").SendAsync(Target(address), Zpl, default);

        Assert.False(outcome.Success);
    }

    [Fact]
    [Trait("Spec", "PJ-2")]
    public async Task AnAllowListedPrinter_ReceivesTheExactZplBytes()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var memory = new MemoryStream();
            await client.GetStream().CopyToAsync(memory);
            return memory.ToArray();
        });

        var outcome = await ZplPrinter("127.0.0.1").SendAsync(Target($"127.0.0.1:{port}"), Zpl, default);

        Assert.True(outcome.Success, outcome.Error);
        Assert.Equal(Zpl.Content, await received.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    [Trait("Spec", "PJ-4")]
    public async Task APrinterThatCannotBeReached_IsAFailedOutcome_NotAnException()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var closedPort = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop(); // nothing listens here any more

        var outcome = await ZplPrinter("127.0.0.1").SendAsync(Target($"127.0.0.1:{closedPort}"), Zpl, default);

        Assert.False(outcome.Success);
        Assert.Contains("could not be reached", outcome.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "PJ-12")]
    public async Task TheFakePrinters_AreRefusedUnlessFakePrintersAreAllowed()
    {
        var storage = new MemoryStorage();
        var refused = await new PdfPreviewLabelPrinter(storage, Options.Create(new LabellingOptions { AllowFakePrinters = false }))
            .SendAsync(Target("memory", PrintFormat.Pdf), Pdf, default);

        Assert.False(refused.Success);
        Assert.Empty(storage.Saved);
    }

    [Fact]
    [Trait("Spec", "PJ-2")]
    public async Task ThePreviewPrinter_StoresThePdfUnderAGeneratedKey_AndTheJamAddressAlwaysFails()
    {
        var storage = new MemoryStorage();
        var printer = new PdfPreviewLabelPrinter(storage, Options.Create(new LabellingOptions { AllowFakePrinters = true }));

        var ok = await printer.SendAsync(Target("memory", PrintFormat.Pdf), Pdf, default);
        var jam = await printer.SendAsync(Target("jam", PrintFormat.Pdf), Pdf, default);

        Assert.True(ok.Success);
        Assert.Equal("key-1", ok.OutputReference);
        Assert.Equal(Pdf.Content, Assert.Single(storage.Saved));
        Assert.False(jam.Success);
        Assert.Contains("jam", jam.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheRouter_SendsZplToTheZplPrinter_AndEverythingElseToThePreviewPrinter()
    {
        var storage = new MemoryStorage();
        var options = Options.Create(new LabellingOptions { AllowFakePrinters = true, AllowedPrinterHosts = ["printer.lan"] });
        var router = new LabelPrinterRouter(new PdfPreviewLabelPrinter(storage, options), new ZplLabelPrinter(options));

        var pdf = await router.SendAsync(Target("memory", PrintFormat.Pdf), Pdf, default);
        var zpl = await router.SendAsync(Target("evil.example:9100"), Zpl, default);

        Assert.True(pdf.Success);
        Assert.False(zpl.Success);
        Assert.Contains("allowed", zpl.Error, StringComparison.OrdinalIgnoreCase);
    }
}
