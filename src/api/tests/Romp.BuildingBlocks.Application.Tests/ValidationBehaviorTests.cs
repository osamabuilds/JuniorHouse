using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Romp.BuildingBlocks.Application.Tests;

// Top-level (not nested) so FluentValidation's and MediatR's assembly-scanning registration
// finds them - a nested private class isn't picked up by either scanner.
internal sealed record TestCommand(string Name) : IRequest<string>;

internal sealed class TestCommandValidator : AbstractValidator<TestCommand>
{
    public TestCommandValidator() => RuleFor(c => c.Name).NotEmpty().WithMessage("Name is required.");
}

internal sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
{
    public Task<string> Handle(TestCommand request, CancellationToken cancellationToken) =>
        Task.FromResult($"Hello, {request.Name}");
}

/// <summary>
/// SCRUM-171, AC-15: proves ValidationBehavior stops an invalid command before its handler runs
/// and surfaces field-level errors. Uses a throwaway command/validator/handler built purely for
/// this test - no real module command exists yet. Romp.Api's ValidationExceptionHandler (outside
/// this project's boundary) is what turns the exception this behaviour throws into the actual
/// ProblemDetails HTTP response; see Romp.Api.IntegrationTests for that half.
/// </summary>
public sealed class ValidationBehaviorTests
{
    private static IMediator BuildMediator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // Validator classes are `internal` by convention (implementation detail, never referenced
        // by name outside their module) - FluentValidation's scanner skips non-public types unless
        // told otherwise.
        services.AddValidatorsFromAssemblyContaining<TestCommandValidator>(includeInternalTypes: true);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<TestCommandHandler>();
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact]
    [Trait("Spec", "SCRUM-171")]
    public async Task Handle_InvalidCommand_ReturnsProblemDetailsWithFieldErrors()
    {
        var mediator = BuildMediator();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => mediator.Send(new TestCommand(string.Empty)));

        Assert.Contains("Name", exception.Errors.Keys);
        Assert.Contains("Name is required.", exception.Errors["Name"]);
        Assert.Equal("Name is required.", exception.Message); // never a generic "one or more errors" line
    }

    [Fact]
    [Trait("Spec", "SCRUM-171")]
    public async Task Handle_ValidCommand_InvokesHandler()
    {
        var mediator = BuildMediator();

        var result = await mediator.Send(new TestCommand("Romp"));

        Assert.Equal("Hello, Romp", result);
    }
}
