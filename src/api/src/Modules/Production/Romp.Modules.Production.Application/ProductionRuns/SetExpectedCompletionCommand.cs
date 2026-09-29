using FluentValidation;
using MediatR;
using Romp.Modules.Production.Application.Abstractions;

namespace Romp.Modules.Production.Application.ProductionRuns;

/// <summary>AC-15 / AC-16: set or move the expected completion date once cutting has started; the history keeps every value.</summary>
public sealed record SetExpectedCompletionCommand(long RunId, DateOnly Date, string? Reason, string ActingPerson) : IRequest<ExpectedCompletionChangeDto>, IProductionCommand;

public sealed class SetExpectedCompletionCommandValidator : AbstractValidator<SetExpectedCompletionCommand>
{
    public SetExpectedCompletionCommandValidator()
    {
        RuleFor(c => c.ActingPerson).ActingPerson();
        RuleFor(c => c.Reason).MaximumLength(500);
    }
}

public sealed class SetExpectedCompletionCommandHandler(IProductionDbContext dbContext)
    : IRequestHandler<SetExpectedCompletionCommand, ExpectedCompletionChangeDto>
{
    public async Task<ExpectedCompletionChangeDto> Handle(SetExpectedCompletionCommand request, CancellationToken cancellationToken)
    {
        var run = await dbContext.LoadRunAsync(request.RunId, cancellationToken);
        var change = run.SetExpectedCompletion(request.Date, request.Reason, request.ActingPerson);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ExpectedCompletionChangeDto(change.OldDate, change.NewDate, change.Reason, change.ActingPerson, change.InsrDte);
    }
}
