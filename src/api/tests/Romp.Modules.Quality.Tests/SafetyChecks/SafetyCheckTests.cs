using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Quality.Application.Batches;
using Romp.Modules.Quality.Application.SafetyChecks;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Domain.Batches;
using Romp.Modules.Quality.Domain.Inspections;
using Romp.Modules.Quality.Domain.Inspections.Aql;
using Romp.Modules.Quality.Domain.SafetyChecks;
using Romp.Modules.Quality.Tests.Support;

namespace Romp.Modules.Quality.Tests.SafetyChecks;

public sealed class SafetyResultTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);
    private static readonly AqlPlanTable Table = new(AqlSeedData.SampleRows(), AqlSeedData.LimitRows());

    private static (QcBatch Batch, Inspection Inspection) InspectedBatch()
    {
        var batch = QcBatch.OpenFor(1, 7, "PO-2026-00007", [((short)1, (short)10, 600), ((short)2, (short)10, 400)])!;
        var plan = AqlCalculator.Plan(Table, batch.LotQty, InspectionLevel.II, AqlLevel.Aql250, 50);
        var inspection = batch.RecordInspection(
            new InspectionInput(null, Today, "Ali Raza", null, InspectionLevel.II, AqlLevel.Aql250,
                CheckType.All.Select(t => new CheckInput(t, CheckResult.Pass)).ToList(), []),
            plan, 0, Today);
        return (batch, inspection);
    }

    private static SafetyInput Pass(short type) => new(type, CheckResult.Pass, null, []);

    private static List<SafetyInput> BothPass() => [Pass(SafetyType.ChokingHazardTrims), Pass(SafetyType.DrawstringNeckline)];

    [Fact]
    [Trait("Spec", "AC-31")]
    public void Record_BothChecks_Stored()
    {
        var (batch, inspection) = InspectedBatch();

        batch.RecordSafetyChecks(inspection.Id, BothPass(), "Ali Raza");

        Assert.Equal(2, inspection.SafetyChecks.Count);
        Assert.All(inspection.SafetyChecks, s => Assert.Equal("Ali Raza", s.ActingPerson));
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public void Record_MissingCheck_Refused()
    {
        var (batch, inspection) = InspectedBatch();

        var error = Assert.Throws<DomainException>(() => batch.RecordSafetyChecks(inspection.Id, [Pass(SafetyType.ChokingHazardTrims)], "Ali Raza"));

        Assert.Contains("both child-safety checks", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public void Record_DuplicateOrUnknownCheck_Refused()
    {
        var (batch, inspection) = InspectedBatch();

        Assert.Throws<DomainException>(() => batch.RecordSafetyChecks(
            inspection.Id, [Pass(SafetyType.ChokingHazardTrims), Pass(SafetyType.ChokingHazardTrims), Pass(SafetyType.DrawstringNeckline)], "Ali Raza"));
        Assert.Throws<DomainException>(() => batch.RecordSafetyChecks(
            inspection.Id, [.. BothPass(), new SafetyInput(9, CheckResult.Pass, null, [])], "Ali Raza"));
    }

    [Theory]
    [Trait("Spec", "AC-33")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Record_NotApplicableWithoutReason_Refused(string? reason)
    {
        var (batch, inspection) = InspectedBatch();
        var checks = new List<SafetyInput> { new(SafetyType.ChokingHazardTrims, CheckResult.NotApplicable, reason, []), Pass(SafetyType.DrawstringNeckline) };

        Assert.Throws<DomainException>(() => batch.RecordSafetyChecks(inspection.Id, checks, "Ali Raza"));
    }

    [Fact]
    [Trait("Spec", "AC-33")]
    public void Record_NotApplicableWithReason_Stored()
    {
        var (batch, inspection) = InspectedBatch();
        var checks = new List<SafetyInput>
        {
            new(SafetyType.ChokingHazardTrims, CheckResult.NotApplicable, "no small trims on this garment", []),
            Pass(SafetyType.DrawstringNeckline),
        };

        batch.RecordSafetyChecks(inspection.Id, checks, "Ali Raza");

        Assert.Equal("no small trims on this garment", inspection.SafetyChecks.Single(s => s.SafetyTypeId == SafetyType.ChokingHazardTrims).Reason);
    }

    [Fact]
    [Trait("Spec", "AC-32")]
    public void Record_FailWithLines_StoresAffected()
    {
        var (batch, inspection) = InspectedBatch();
        var checks = new List<SafetyInput>
        {
            new(SafetyType.ChokingHazardTrims, CheckResult.Fail, null, [new LineRef(1, 10)]),
            Pass(SafetyType.DrawstringNeckline),
        };

        batch.RecordSafetyChecks(inspection.Id, checks, "Ali Raza");

        var failed = inspection.SafetyChecks.Single(s => s.SafetyTypeId == SafetyType.ChokingHazardTrims);
        Assert.Equal((1, 10), Assert.Single(failed.AffectedLines) is { } line ? ((int)line.SizeId, (int)line.ColourId) : default);
    }

    [Fact]
    [Trait("Spec", "AC-32")]
    public void Record_FailWithALineNotInTheBatch_Refused()
    {
        var (batch, inspection) = InspectedBatch();
        var checks = new List<SafetyInput>
        {
            new(SafetyType.ChokingHazardTrims, CheckResult.Fail, null, [new LineRef(9, 99)]),
            Pass(SafetyType.DrawstringNeckline),
        };

        Assert.Throws<DomainException>(() => batch.RecordSafetyChecks(inspection.Id, checks, "Ali Raza"));
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public void Record_Again_ReplacesTheEntryWithoutDuplicates()
    {
        var (batch, inspection) = InspectedBatch();
        batch.RecordSafetyChecks(inspection.Id, [new(SafetyType.ChokingHazardTrims, CheckResult.Fail, null, [new LineRef(1, 10)]), Pass(SafetyType.DrawstringNeckline)], "Ali Raza");

        batch.RecordSafetyChecks(inspection.Id, [Pass(SafetyType.ChokingHazardTrims), Pass(SafetyType.DrawstringNeckline)], "Sara Khan");

        Assert.Equal(2, inspection.SafetyChecks.Count);
        Assert.All(inspection.SafetyChecks, s =>
        {
            Assert.Equal(CheckResult.Pass, s.ResultId);
            Assert.Empty(s.AffectedLines);
            Assert.Equal("Sara Khan", s.ActingPerson);
        });
    }

    [Theory]
    [Trait("Spec", "AC-30")]
    [InlineData("")]
    [InlineData("A")]
    public void Record_NoActingPerson_Refused(string person)
    {
        var (batch, inspection) = InspectedBatch();

        Assert.Throws<DomainException>(() => batch.RecordSafetyChecks(inspection.Id, BothPass(), person));
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public void Record_UnknownInspection_NotFound()
    {
        var (batch, _) = InspectedBatch();

        Assert.Throws<KeyNotFoundException>(() => batch.RecordSafetyChecks(999, BothPass(), "Ali Raza"));
    }
}

public sealed class SafetyGateTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);
    private static readonly AqlPlanTable Table = new(AqlSeedData.SampleRows(), AqlSeedData.LimitRows());
    private static readonly IReadOnlyCollection<LineRef> AllLines = [new LineRef(1, 10), new LineRef(2, 10)];

    private static Inspection Inspected(params SafetyInput[] safety)
    {
        var batch = QcBatch.OpenFor(1, 7, "PO-2026-00007", [((short)1, (short)10, 600), ((short)2, (short)10, 400)])!;
        var plan = AqlCalculator.Plan(Table, batch.LotQty, InspectionLevel.II, AqlLevel.Aql250, 50);
        var inspection = batch.RecordInspection(
            new InspectionInput(null, Today, "Ali Raza", null, InspectionLevel.II, AqlLevel.Aql250,
                CheckType.All.Select(t => new CheckInput(t, CheckResult.Pass)).ToList(), []),
            plan, 0, Today);
        if (safety.Length > 0)
        {
            batch.RecordSafetyChecks(inspection.Id, safety, "Ali Raza");
        }

        return inspection;
    }

    private static SafetyInput Pass(short type) => new(type, CheckResult.Pass, null, []);

    private static SafetyInput Fail(short type, params LineRef[] lines) => new(type, CheckResult.Fail, null, lines);

    private static SafetyInput[] BothPass() => [Pass(SafetyType.ChokingHazardTrims), Pass(SafetyType.DrawstringNeckline)];

    [Fact]
    [Trait("Spec", "AC-31")]
    public void Ensure_MissingCheck_Refused()
    {
        var error = Assert.Throws<DomainException>(() => SafetyGate.EnsureCanRelease(QcOutcome.Accepted, Inspected(), AllLines));

        Assert.Contains("choking hazard", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public void Ensure_NoInspection_Refused()
    {
        Assert.Throws<DomainException>(() => SafetyGate.EnsureCanRelease(QcOutcome.Concession, null, AllLines));
    }

    [Theory]
    [Trait("Spec", "AC-31")]
    [InlineData(QcOutcome.Accepted)]
    [InlineData(QcOutcome.Concession)]
    public void Ensure_BothPassed_AllowsAcceptAndConcession(short outcome)
    {
        SafetyGate.EnsureCanRelease(outcome, Inspected(BothPass()), AllLines);
    }

    [Fact]
    [Trait("Spec", "AC-33")]
    public void Ensure_NotApplicable_Allowed()
    {
        var checks = new[] { new SafetyInput(SafetyType.ChokingHazardTrims, CheckResult.NotApplicable, "no trims", []), Pass(SafetyType.DrawstringNeckline) };

        SafetyGate.EnsureCanRelease(QcOutcome.Accepted, Inspected(checks), AllLines);
    }

    [Theory]
    [Trait("Spec", "AC-32")]
    [InlineData(QcOutcome.Accepted)]
    [InlineData(QcOutcome.Concession)]
    public void Ensure_FailedAffectedLine_AcceptOrConcessionRefused(short outcome)
    {
        var inspection = Inspected(Fail(SafetyType.DrawstringNeckline, new LineRef(1, 10)), Pass(SafetyType.ChokingHazardTrims));

        var error = Assert.Throws<DomainException>(() => SafetyGate.EnsureCanRelease(outcome, inspection, [new LineRef(1, 10)]));

        Assert.Contains("drawstring", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "AC-32")]
    public void Ensure_FailedOtherLines_AcceptAllowedForUnaffected()
    {
        var inspection = Inspected(Fail(SafetyType.ChokingHazardTrims, new LineRef(1, 10)), Pass(SafetyType.DrawstringNeckline));

        SafetyGate.EnsureCanRelease(QcOutcome.Accepted, inspection, [new LineRef(2, 10)]);
    }

    [Theory]
    [Trait("Spec", "AC-32")]
    [InlineData(QcOutcome.Rejected)]
    [InlineData(QcOutcome.Rework)]
    public void Ensure_FailedLine_RejectAndReworkAllowed(short outcome)
    {
        var inspection = Inspected(Fail(SafetyType.ChokingHazardTrims, new LineRef(1, 10)), Pass(SafetyType.DrawstringNeckline));

        SafetyGate.EnsureCanRelease(outcome, inspection, [new LineRef(1, 10)]);
    }

    [Fact]
    [Trait("Spec", "AC-32")]
    public void Ensure_FailWithoutLines_BlocksTheWholeBatch()
    {
        var inspection = Inspected(Fail(SafetyType.ChokingHazardTrims), Pass(SafetyType.DrawstringNeckline));

        Assert.Throws<DomainException>(() => SafetyGate.EnsureCanRelease(QcOutcome.Accepted, inspection, [new LineRef(2, 10)]));
        SafetyGate.EnsureCanRelease(QcOutcome.Rejected, inspection, AllLines);
    }

    [Fact]
    [Trait("Spec", "AC-34")]
    public void Ensure_NewRoundWithoutChecks_Refused()
    {
        // A rework return opens a new inspection round; it starts with no safety results, whatever the first round recorded.
        var firstRound = Inspected(BothPass());
        var secondRound = Inspected();

        SafetyGate.EnsureCanRelease(QcOutcome.Accepted, firstRound, AllLines);
        Assert.Throws<DomainException>(() => SafetyGate.EnsureCanRelease(QcOutcome.Accepted, secondRound, AllLines));
    }

    [Fact]
    [Trait("Spec", "AC-32")]
    public void NoBypassExposed_ReflectionCheck()
    {
        var methods = typeof(SafetyGate).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        var gate = Assert.Single(methods);
        Assert.Equal("EnsureCanRelease", gate.Name);
        Assert.DoesNotContain(gate.GetParameters(), p => p.ParameterType == typeof(bool) || p.Name!.Contains("override", StringComparison.OrdinalIgnoreCase)
            || p.Name.Contains("force", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("skip", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class RecordSafetyChecksCommandHandlerTests
{
    private static SafetyCheckDto Pass(short type) => new(type, CheckResult.Pass, null, []);

    private static async Task<(TestHost Host, long InspectionId)> InspectedAsync(string name)
    {
        var host = new TestHost(name);
        var (_, batchId) = await host.RecordGrnAsync(line1: 600, line2: 400);
        var inspection = await host.SendAsync(InspectionData.Command(batchId));
        return (host, inspection.Id);
    }

    [Fact]
    [Trait("Spec", "AC-30")]
    [Trait("Spec", "AC-31")]
    public async Task Handle_Valid_Stored()
    {
        var (host, inspectionId) = await InspectedAsync(nameof(Handle_Valid_Stored));
        using (host)
        {
            var result = await host.SendAsync(new RecordSafetyChecksCommand(
                inspectionId, "Sara Khan", [Pass(SafetyType.ChokingHazardTrims), new(SafetyType.DrawstringNeckline, CheckResult.Fail, null, [new SafetyLineDto(1, 10)])]));

            Assert.Equal(2, result.SafetyChecks.Count);
            var failed = result.SafetyChecks.Single(s => s.SafetyTypeId == SafetyType.DrawstringNeckline);
            Assert.Equal(CheckResult.Fail, failed.ResultId);
            Assert.Equal(new SafetyLineDto(1, 10), Assert.Single(failed.AffectedLines));
            var stored = await host.WithDbAsync(db => db.SafetyChecks.AsNoTracking().ToListAsync());
            Assert.All(stored, s => Assert.Equal("Sara Khan", s.ActingPerson));
        }
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public async Task Handle_Repeated_ReplacesTheEntryWithoutDuplicates()
    {
        var (host, inspectionId) = await InspectedAsync(nameof(Handle_Repeated_ReplacesTheEntryWithoutDuplicates));
        using (host)
        {
            await host.SendAsync(new RecordSafetyChecksCommand(
                inspectionId, "Sara Khan", [new(SafetyType.ChokingHazardTrims, CheckResult.Fail, null, [new SafetyLineDto(1, 10)]), Pass(SafetyType.DrawstringNeckline)]));

            var second = await host.SendAsync(new RecordSafetyChecksCommand(
                inspectionId, "Ali Raza", [Pass(SafetyType.ChokingHazardTrims), Pass(SafetyType.DrawstringNeckline)]));

            Assert.All(second.SafetyChecks, s => Assert.Equal(CheckResult.Pass, s.ResultId));
            Assert.Equal(2, await host.WithDbAsync(db => db.SafetyChecks.CountAsync()));
            Assert.Equal(0, await host.WithDbAsync(db => db.Set<SafetyAffectedLine>().CountAsync()));
        }
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public async Task Handle_MissingCheck_RefusedAndNothingStored()
    {
        var (host, inspectionId) = await InspectedAsync(nameof(Handle_MissingCheck_RefusedAndNothingStored));
        using (host)
        {
            await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(
                new RecordSafetyChecksCommand(inspectionId, "Sara Khan", [Pass(SafetyType.ChokingHazardTrims)])));

            Assert.Equal(0, await host.WithDbAsync(db => db.SafetyChecks.CountAsync()));
        }
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public async Task Handle_InvalidInput_RefusedWithFieldErrors()
    {
        var (host, inspectionId) = await InspectedAsync(nameof(Handle_InvalidInput_RefusedWithFieldErrors));
        using (host)
        {
            var error = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new RecordSafetyChecksCommand(inspectionId, "A", [])));

            Assert.Contains(error.Errors.Keys, k => k.Contains("ActingPerson", StringComparison.Ordinal));
            Assert.Contains(error.Errors.Keys, k => k.Contains("Checks", StringComparison.Ordinal));
        }
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public async Task Handle_UnknownInspection_NotFound()
    {
        using var host = new TestHost(nameof(Handle_UnknownInspection_NotFound));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(
            new RecordSafetyChecksCommand(999, "Sara Khan", [Pass(SafetyType.ChokingHazardTrims), Pass(SafetyType.DrawstringNeckline)])));
    }
}
