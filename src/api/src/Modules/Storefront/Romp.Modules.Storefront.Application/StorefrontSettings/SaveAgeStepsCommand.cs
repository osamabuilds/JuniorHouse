using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Application.StorefrontSettings;

/// <summary>
/// Saves the "Shop by age" list. An age code is the key used in links and is fixed once published: it can be disabled but not removed
/// or renamed, and renaming a label never changes a URL (ST-2, ST-4).
/// </summary>
public sealed record SaveAgeStepsCommand(int ExpectedVersion, IReadOnlyList<AgeStepDto> Steps)
    : IRequest<SavedDto<IReadOnlyList<AgeStepDto>>>, IStorefrontCommand;

public sealed class SaveAgeStepsCommandValidator : AbstractValidator<SaveAgeStepsCommand>
{
    public SaveAgeStepsCommandValidator() => RuleFor(c => c.Steps).NotNull();
}

public sealed class SaveAgeStepsCommandHandler(IStorefrontDbContext db, IExternalHostPolicy hosts)
    : IRequestHandler<SaveAgeStepsCommand, SavedDto<IReadOnlyList<AgeStepDto>>>
{
    public async Task<SavedDto<IReadOnlyList<AgeStepDto>>> Handle(SaveAgeStepsCommand request, CancellationToken cancellationToken)
    {
        var loaded = await DraftSupport.LoadAsync(db, hosts, cancellationToken);
        loaded.Draft.Advance(request.ExpectedVersion);

        var steps = request.Steps.Select(s => new AgeStep(s.Code, s.Label, s.Sort, s.Enabled)).OrderBy(s => s.SortSeq).ToList();
        DraftSupport.ThrowIfInvalid(loaded.Content with { AgeSteps = steps }, "ages");

        var published = await PublishedAgeCodesAsync(cancellationToken);
        var problems = published
            .Where(code => steps.All(s => s.Code != code))
            .Select(code => new SettingsProblem($"ages.{code}", $"The age code \"{code}\" is already published, so it cannot be removed or renamed. Disable it instead."))
            .ToList();
        DraftSupport.ThrowIfAny(problems);

        db.AgeSteps.RemoveRange(loaded.Content.AgeSteps);
        await db.SaveChangesAsync(cancellationToken);
        db.AgeSteps.AddRange(steps);
        await db.SaveChangesAsync(cancellationToken);

        return new SavedDto<IReadOnlyList<AgeStepDto>>(loaded.Draft.Version, [.. steps.Select(SettingsMapper.ToDto)]);
    }

    private async Task<IReadOnlyList<string>> PublishedAgeCodesAsync(CancellationToken cancellationToken)
    {
        var document = await db.Snapshots.AsNoTracking().Where(s => s.IsCurrent).Select(s => s.Document).FirstOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return [];
        }

        using var json = JsonDocument.Parse(document);
        return [.. json.RootElement.GetProperty("settings").GetProperty("ages").EnumerateArray().Select(a => a.GetProperty("code").GetString()!)];
    }
}
