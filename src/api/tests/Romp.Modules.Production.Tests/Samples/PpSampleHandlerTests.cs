using Romp.Modules.Production.Tests.Milestones;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Production.Application.Milestones;
using Romp.Modules.Production.Application.ProductionRuns;
using Romp.Modules.Production.Application.Samples;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Tests.Support;

namespace Romp.Modules.Production.Tests.Samples;

public sealed class PpSampleHandlerTests
{
    private static SubmitPpSampleCommand Submit(long runId) => new(runId, new DateOnly(2026, 9, 5), Runs.Reporter, Runs.Person, "sample sent by courier");

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task Handle_Approve_RecordsApproverAndEnablesCutting()
    {
        using var host = new TestHost(nameof(Handle_Approve_RecordsApproverAndEnablesCutting));
        var runId = await host.OpenRunAsync();
        await host.SendAsync(Submit(runId));

        var round = await host.SendAsync(new DecidePpSampleCommand(runId, 1, Approve: true, null, "  Osama Shafique "));

        Assert.Equal("Approved", round.DecisionCode);
        Assert.Equal("Osama Shafique", round.DecidedBy);
        Assert.NotNull(round.DecidedAt);

        // Cutting is now recordable.
        var cutting = await host.SendAsync(new RecordMilestoneCommand(runId, MilestoneType.BulkCuttingStarted, new DateOnly(2026, 9, 10), Runs.Reporter, Runs.Person, null));
        Assert.Equal("BulkCuttingStarted", cutting.TypeCode);
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task Handle_RejectWithoutReason_FieldError()
    {
        using var host = new TestHost(nameof(Handle_RejectWithoutReason_FieldError));
        var runId = await host.OpenRunAsync();
        await host.SendAsync(Submit(runId));

        var error = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new DecidePpSampleCommand(runId, 1, Approve: false, " ", Runs.Person)));

        Assert.Contains("Reason", error.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task Handle_RejectThenResubmit_ShowsBothRoundsInOrder()
    {
        using var host = new TestHost(nameof(Handle_RejectThenResubmit_ShowsBothRoundsInOrder));
        var runId = await host.OpenRunAsync();
        await host.SendAsync(Submit(runId));
        await host.SendAsync(new DecidePpSampleCommand(runId, 1, false, "collar too wide", Runs.Person));
        var second = await host.SendAsync(Submit(runId));

        var rounds = await host.SendAsync(new GetPpSampleRoundsQuery(runId));

        Assert.Equal(2, second.RoundNo);
        Assert.Equal([1, 2], rounds.Select(r => r.RoundNo).ToArray());
        Assert.Equal(["Rejected", "Pending"], rounds.Select(r => r.DecisionCode).ToArray());
        Assert.Equal("collar too wide", rounds[0].DecisionReason);
    }

    [Fact]
    [Trait("Spec", "AC-13")]
    public async Task Handle_SubmitAfterApproval_WithoutAmendment_Refused()
    {
        using var host = new TestHost(nameof(Handle_SubmitAfterApproval_WithoutAmendment_Refused));
        var runId = await host.OpenRunAsync();
        await RecordMilestoneCommandHandlerTests.ApproveSampleAsync(host, runId);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Submit(runId)));
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task Upload_AValidPhoto_StoredWithAGeneratedKey_AndListedOnTheRound()
    {
        using var host = new TestHost(nameof(Upload_AValidPhoto_StoredWithAGeneratedKey_AndListedOnTheRound));
        var runId = await host.OpenRunAsync();
        await host.SendAsync(Submit(runId));

        var file = await host.SendAsync(new UploadPpSampleFileCommand(runId, 1, "..\\..\\evil name.png", TestFileBytes.Png()));

        Assert.Equal("image/png", file.ContentType);
        Assert.Equal("evil name.png", file.FileName); // path stripped
        Assert.Equal(1, host.Storage.Count);
        var stored = await host.WithDbAsync(db => db.PpFiles.AsNoTracking().SingleAsync());
        Assert.DoesNotContain("evil", stored.StorageKey); // never derived from the client's name
        Assert.Single((await host.SendAsync(new GetPpSampleRoundsQuery(runId)))[0].Files);
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task Upload_DisguisedExecutable_Refused_AndNothingStored()
    {
        using var host = new TestHost(nameof(Upload_DisguisedExecutable_Refused_AndNothingStored));
        var runId = await host.OpenRunAsync();
        await host.SendAsync(Submit(runId));

        var error = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(
            new UploadPpSampleFileCommand(runId, 1, "photo.jpg", TestFileBytes.DisguisedExecutable())));

        Assert.Contains("isn't allowed", error.Message);
        Assert.Equal(0, host.Storage.Count);
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task Upload_OverTheSizeLimit_Refused()
    {
        using var host = new TestHost(nameof(Upload_OverTheSizeLimit_Refused));
        var runId = await host.OpenRunAsync();
        await host.SendAsync(Submit(runId));
        var big = TestFileBytes.Png().Concat(new byte[2048]).ToArray(); // limit in the test host is 1 KB

        var error = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new UploadPpSampleFileCommand(runId, 1, "big.png", big)));

        Assert.Contains("larger than", error.Message);
        Assert.Equal(0, host.Storage.Count);
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task Upload_BeyondTheFileCountLimit_Refused()
    {
        using var host = new TestHost(nameof(Upload_BeyondTheFileCountLimit_Refused));
        var runId = await host.OpenRunAsync();
        await host.SendAsync(Submit(runId));
        await host.SendAsync(new UploadPpSampleFileCommand(runId, 1, "1.png", TestFileBytes.Png()));
        await host.SendAsync(new UploadPpSampleFileCommand(runId, 1, "2.png", TestFileBytes.Png()));

        var error = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new UploadPpSampleFileCommand(runId, 1, "3.png", TestFileBytes.Png())));

        Assert.Contains("maximum of 2", error.Message);
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task Download_HasSafeHeaders_AndCannotCrossRuns()
    {
        using var host = new TestHost(nameof(Download_HasSafeHeaders_AndCannotCrossRuns));
        var runId = await host.OpenRunAsync();
        await host.SendAsync(Submit(runId));
        var file = await host.SendAsync(new UploadPpSampleFileCommand(runId, 1, "photo \"1\".png", TestFileBytes.Png()));

        var download = await host.SendAsync(new GetPpSampleFileQuery(runId, 1, file.Id));

        Assert.Equal("nosniff", download.Headers["X-Content-Type-Options"]);
        Assert.StartsWith("attachment;", download.Headers["Content-Disposition"]);
        Assert.DoesNotContain("\"1\"", download.Headers["Content-Disposition"]); // header-breaking characters removed
        Assert.Equal(TestFileBytes.Png(), download.Content);

        // Another run's id (or round number) must not reach this file (SEC-05 groundwork).
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new GetPpSampleFileQuery(runId + 1, 1, file.Id)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new GetPpSampleFileQuery(runId, 2, file.Id)));
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task Remove_SoftDeletesTheFile_AndItDisappearsFromTheRound()
    {
        using var host = new TestHost(nameof(Remove_SoftDeletesTheFile_AndItDisappearsFromTheRound));
        var runId = await host.OpenRunAsync();
        await host.SendAsync(Submit(runId));
        var file = await host.SendAsync(new UploadPpSampleFileCommand(runId, 1, "a.png", TestFileBytes.Png()));

        await host.SendAsync(new RemovePpSampleFileCommand(runId, 1, file.Id));

        Assert.Empty((await host.SendAsync(new GetPpSampleRoundsQuery(runId)))[0].Files);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new GetPpSampleFileQuery(runId, 1, file.Id)));
    }

    [Fact]
    [Trait("Spec", "AC-29")]
    public async Task Upload_OnACancelledRun_Refused()
    {
        using var host = new TestHost(nameof(Upload_OnACancelledRun_Refused));
        var runId = await host.OpenRunAsync();
        await host.SendAsync(Submit(runId));
        await host.SendAsync(new MarkRunCancelledCommand(FakePurchaseOrderQueries.PoId));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new UploadPpSampleFileCommand(runId, 1, "a.png", TestFileBytes.Png())));
    }
}
