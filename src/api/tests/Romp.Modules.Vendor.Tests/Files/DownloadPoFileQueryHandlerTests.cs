using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application.Files;
using Romp.Modules.Vendor.Tests.Support;

namespace Romp.Modules.Vendor.Tests.Files;

public sealed class DownloadPoFileQueryHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-46")]
    public async Task Handle_ReturnsAttachmentHeadersAndSanitisedFilename()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        var bytes = PoTestFiles.Pdf("content");
        var file = await sender.Send(new UploadPoFileCommand(po.Id, 1, "..\\..\\evil\"; name=x.pdf", bytes));

        var download = await sender.Send(new DownloadPoFileQuery(po.Id, file.Id));

        Assert.Equal(bytes, download.Content);
        Assert.Equal("application/pdf", download.ContentType);
        Assert.Equal("evil name=x.pdf", download.FileName);
        Assert.StartsWith("attachment;", download.Headers["Content-Disposition"]);
        Assert.Equal("nosniff", download.Headers["X-Content-Type-Options"]);
    }

    [Fact]
    [Trait("Spec", "AC-46")]
    public async Task Handle_FileOfAnotherPo_NotFound()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        var file = await sender.Send(new UploadPoFileCommand(po.Id, 1, "spec.pdf", PoTestFiles.Pdf()));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sender.Send(new DownloadPoFileQuery(po.Id + 999, file.Id)));
    }
}
