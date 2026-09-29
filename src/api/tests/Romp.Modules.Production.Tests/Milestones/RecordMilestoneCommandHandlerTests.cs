using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Production.Application.Milestones;
using Romp.Modules.Production.Application.ProductionRuns;
using Romp.Modules.Production.Application.Samples;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Tests.Support;

namespace Romp.Modules.Production.Tests.Milestones;

public sealed class RecordMilestoneCommandHandlerTests
{
    private static RecordMilestoneCommand Command(long runId, short type, DateOnly? date = null, string person = Runs.Person, IReadOnlyList<QuantityLineDto>? lines = null) =>
        new(runId, type, date ?? new DateOnly(2026, 9, 3), Runs.Reporter, person, null, lines);

    internal static async Task ApproveSampleAsync(TestHost host, long runId)
    {
        await host.SendAsync(new SubmitPpSampleCommand(runId, new DateOnly(2026, 9, 5), Runs.Reporter, Runs.Person, null));
        await host.SendAsync(new DecidePpSampleCommand(runId, 1, true, null, Runs.Person));
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public async Task Handle_StoresTheMilestoneWithAuditAndActingPerson()
    {
        using var host = new TestHost(nameof(Handle_StoresTheMilestoneWithAuditAndActingPerson));
        var runId = await host.OpenRunAsync();

        var dto = await host.SendAsync(Command(runId, MilestoneType.FabricBooked));

        Assert.True(dto.Id > 0);
        Assert.Equal("FabricBooked", dto.TypeCode);
        Assert.Equal(Runs.Person, dto.ActingPerson);
        Assert.Equal(Runs.Reporter, dto.ReportedBy);
        Assert.False(string.IsNullOrEmpty(dto.RecordedBy)); // the system actor, stamped by the audit interceptor
        Assert.NotEqual(default, dto.RecordedAt);
    }

    [Theory]
    [Trait("Spec", "AC-35")]
    [InlineData("")]
    [InlineData("A")]
    public async Task Handle_ActingPersonMissing_FieldError(string person)
    {
        using var host = new TestHost($"{nameof(Handle_ActingPersonMissing_FieldError)}{person}");
        var runId = await host.OpenRunAsync();

        var error = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(Command(runId, MilestoneType.FabricBooked, person: person)));

        Assert.Contains("ActingPerson", error.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task Handle_DateBeforeThePoWasSent_Refused()
    {
        using var host = new TestHost(nameof(Handle_DateBeforeThePoWasSent_Refused));
        var runId = await host.OpenRunAsync();

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Command(runId, MilestoneType.FabricBooked, new DateOnly(2026, 8, 30))));
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public async Task Handle_RecordedMoreThanThreeDaysLate_SavedWithTheFlag()
    {
        using var host = new TestHost(nameof(Handle_RecordedMoreThanThreeDaysLate_SavedWithTheFlag));
        var runId = await host.OpenRunAsync();

        var late = await host.SendAsync(Command(runId, MilestoneType.FabricBooked, TestHost.Today.AddDays(-4)));
        var onTime = await host.SendAsync(Command(runId, MilestoneType.FabricBooked, TestHost.Today.AddDays(-3)));

        Assert.True(late.IsRecordedLate);
        Assert.False(onTime.IsRecordedLate);
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task Handle_BulkCuttingWithoutAnApprovedSample_RefusedAndNothingSaved()
    {
        using var host = new TestHost(nameof(Handle_BulkCuttingWithoutAnApprovedSample_RefusedAndNothingSaved));
        var runId = await host.OpenRunAsync();

        var error = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Command(runId, MilestoneType.BulkCuttingStarted)));

        Assert.Contains("pre-production sample has been approved", error.Message);
        Assert.Equal(0, await host.WithDbAsync(db => db.Milestones.CountAsync()));
        Assert.DoesNotContain("ProductionStartedEvent", await host.WithDbAsync(db => db.Set<OutboxMessage>().Select(m => m.EventType).ToListAsync()));
    }

    [Fact]
    [Trait("Spec", "AC-30")]
    public async Task Handle_BulkCutting_WritesMilestoneAndProductionStartedInOneTransaction()
    {
        using var host = new TestHost(nameof(Handle_BulkCutting_WritesMilestoneAndProductionStartedInOneTransaction));
        var runId = await host.OpenRunAsync();
        await ApproveSampleAsync(host, runId);

        await host.SendAsync(Command(runId, MilestoneType.BulkCuttingStarted));

        var types = await host.WithDbAsync(db => db.Set<OutboxMessage>().Select(m => m.EventType).ToListAsync());
        Assert.Contains("ProductionStartedEvent", types);
        Assert.Contains("ProductionMilestoneReachedEvent", types);
        Assert.Equal(1, await host.WithDbAsync(db => db.Milestones.CountAsync(m => m.TypeId == MilestoneType.BulkCuttingStarted)));

        var started = await host.WithDbAsync(db => db.Set<OutboxMessage>().SingleAsync(m => m.EventType == "ProductionStartedEvent"));
        Assert.Equal(runId, started.AggregateId); // per-aggregate ordering key (ADR 0004)
        Assert.Equal("ProductionRun", started.AggregateType);
    }

    [Fact]
    [Trait("Spec", "AC-17")]
    public async Task Handle_FinishedQuantityForASizeNotOnThePo_FieldError()
    {
        using var host = new TestHost(nameof(Handle_FinishedQuantityForASizeNotOnThePo_FieldError));
        var runId = await host.OpenRunAsync();

        var error = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(
            Command(runId, MilestoneType.FinishedReadyToShip, lines: [new QuantityLineDto(9, 99, 10)])));

        Assert.Contains("FinishedLines", error.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-17")]
    public async Task Handle_FinishedQuantityOnThePo_StoredPerSizeAndColour()
    {
        using var host = new TestHost(nameof(Handle_FinishedQuantityOnThePo_StoredPerSizeAndColour));
        var runId = await host.OpenRunAsync();

        var dto = await host.SendAsync(Command(runId, MilestoneType.FinishedReadyToShip, lines: [new QuantityLineDto(1, 10, 80), new QuantityLineDto(2, 10, 40)]));

        Assert.Equal(2, dto.FinishedLines.Count);
        Assert.Equal(120, dto.FinishedLines.Sum(l => l.Qty));
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task Handle_PpTypeAsAPlainMilestone_FieldError()
    {
        using var host = new TestHost(nameof(Handle_PpTypeAsAPlainMilestone_FieldError));
        var runId = await host.OpenRunAsync();

        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(Command(runId, MilestoneType.PpSampleApproved)));
    }
}
