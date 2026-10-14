using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Production.Application.Abstractions;
using Romp.Modules.Production.Application.ProductionRuns;

namespace Romp.Modules.Production.Application.Samples;

/// <summary>AC-11 / AC-13: staff record that the vendor submitted a pre-production sample (a new round; no limit on rounds, D6).</summary>
public sealed record SubmitPpSampleCommand(
    long RunId, DateOnly? SubmittedDate, string ReportedBy, string ActingPerson, string? Note) : IRequest<PpSampleRoundDto>, IProductionCommand;

public sealed class SubmitPpSampleCommandValidator : AbstractValidator<SubmitPpSampleCommand>
{
    public SubmitPpSampleCommandValidator()
    {
        RuleFor(c => c.ReportedBy).NotEmpty().WithMessage("Enter who at the vendor sent the sample.").MaximumLength(100);
        RuleFor(c => c.ActingPerson).ActingPerson();
        RuleFor(c => c.Note).MaximumLength(500);
    }
}

public sealed class SubmitPpSampleCommandHandler(IProductionDbContext dbContext, ProductionOptions options, TimeProvider timeProvider)
    : IRequestHandler<SubmitPpSampleCommand, PpSampleRoundDto>
{
    public async Task<PpSampleRoundDto> Handle(SubmitPpSampleCommand request, CancellationToken cancellationToken)
    {
        var run = await dbContext.LoadRunAsync(request.RunId, cancellationToken);
        var today = PakistanClock.Today(timeProvider);

        var round = run.SubmitPpSample(request.SubmittedDate ?? today, request.ReportedBy, request.ActingPerson, request.Note, today, options.RecordedLateDays);
        await dbContext.SaveChangesAsync(cancellationToken);
        return round.ToDto();
    }
}

/// <summary>AC-10 / AC-11: approve or reject the pending round. Rejection needs a reason; approval unlocks bulk cutting.</summary>
public sealed record DecidePpSampleCommand(
    long RunId, int RoundNo, bool Approve, string? Reason, string ActingPerson) : IRequest<PpSampleRoundDto>, IProductionCommand;

public sealed class DecidePpSampleCommandValidator : AbstractValidator<DecidePpSampleCommand>
{
    public DecidePpSampleCommandValidator()
    {
        RuleFor(c => c.ActingPerson).ActingPerson();
        RuleFor(c => c.Reason).MaximumLength(500);
        RuleFor(c => c.Reason).NotEmpty().When(c => !c.Approve).WithMessage("A reason is required when a sample is rejected.");
    }
}

public sealed class DecidePpSampleCommandHandler(IProductionDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<DecidePpSampleCommand, PpSampleRoundDto>
{
    public async Task<PpSampleRoundDto> Handle(DecidePpSampleCommand request, CancellationToken cancellationToken)
    {
        var run = await dbContext.LoadRunAsync(request.RunId, cancellationToken);
        var round = run.DecidePpSample(
            request.RoundNo, request.Approve, request.ActingPerson, request.Reason, PakistanClock.Today(timeProvider), timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken);
        return round.ToDto();
    }
}

/// <summary>Lists the rounds with their files (the run detail carries the same; this serves the round panel on its own).</summary>
public sealed record GetPpSampleRoundsQuery(long RunId) : IRequest<IReadOnlyList<PpSampleRoundDto>>;

public sealed class GetPpSampleRoundsQueryHandler(IProductionDbContext dbContext)
    : IRequestHandler<GetPpSampleRoundsQuery, IReadOnlyList<PpSampleRoundDto>>
{
    public async Task<IReadOnlyList<PpSampleRoundDto>> Handle(GetPpSampleRoundsQuery request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Runs.AnyAsync(r => r.Id == request.RunId, cancellationToken))
        {
            throw new KeyNotFoundException($"Production run {request.RunId} was not found.");
        }

        var rounds = await dbContext.PpRounds.AsNoTracking().Include(r => r.Files)
            .Where(r => r.RunId == request.RunId).OrderBy(r => r.RoundNo).ToListAsync(cancellationToken);
        return rounds.Select(r => r.ToDto()).ToList();
    }
}
