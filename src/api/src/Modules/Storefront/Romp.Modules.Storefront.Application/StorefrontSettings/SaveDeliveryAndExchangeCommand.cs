using FluentValidation;
using MediatR;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Application.StorefrontSettings;

/// <summary>
/// Saves the delivery table (one row per city: days, charge in PKR, free-delivery amount, courier) and the exchange policy (window and
/// conditions). Negative money and a longest time shorter than the shortest are refused (ST-4, ST-7).
/// </summary>
public sealed record SaveDeliveryAndExchangeCommand(int ExpectedVersion, IReadOnlyList<DeliveryRuleDto> Rules, ExchangeDto? Exchange)
    : IRequest<SavedDto<DeliveryAndExchangeDto>>, IStorefrontCommand;

public sealed record DeliveryAndExchangeDto(IReadOnlyList<DeliveryRuleDto> Rules, ExchangeDto? Exchange);

public sealed class SaveDeliveryAndExchangeCommandValidator : AbstractValidator<SaveDeliveryAndExchangeCommand>
{
    public SaveDeliveryAndExchangeCommandValidator() => RuleFor(c => c.Rules).NotNull();
}

public sealed class SaveDeliveryAndExchangeCommandHandler(IStorefrontDbContext db, IExternalHostPolicy hosts)
    : IRequestHandler<SaveDeliveryAndExchangeCommand, SavedDto<DeliveryAndExchangeDto>>
{
    public async Task<SavedDto<DeliveryAndExchangeDto>> Handle(SaveDeliveryAndExchangeCommand request, CancellationToken cancellationToken)
    {
        var loaded = await DraftSupport.LoadAsync(db, hosts, cancellationToken);
        loaded.Draft.Advance(request.ExpectedVersion);

        var rules = request.Rules.Select(SettingsMapper.ToEntity).ToList();
        var exchange = request.Exchange is null ? null : new ExchangePolicy(request.Exchange.WindowDays, request.Exchange.Conditions ?? []);

        var problems = rules.GroupBy(r => r.City, StringComparer.OrdinalIgnoreCase).Where(g => g.Key.Length > 0 && g.Count() > 1)
            .Select(g => new SettingsProblem($"delivery.{g.Key}", $"{g.Key} is listed twice. Keep one row per city."))
            .ToList();
        if (exchange is not null)
        {
            problems.AddRange(exchange.Conditions.Where(c => c.Text.Length is 0 or > 300)
                .Select(_ => new SettingsProblem("exchange.conditions", "Each exchange condition must be 1 to 300 characters.")));
        }

        DraftSupport.ThrowIfAny(problems);
        DraftSupport.ThrowIfInvalid(loaded.Content with { DeliveryRules = rules, Exchange = exchange }, "delivery", "exchange");

        db.DeliveryRules.RemoveRange(loaded.Content.DeliveryRules);
        if (loaded.Content.Exchange is not null)
        {
            db.ExchangePolicies.Remove(loaded.Content.Exchange);
        }

        await db.SaveChangesAsync(cancellationToken);
        db.DeliveryRules.AddRange(rules);
        if (exchange is not null)
        {
            db.ExchangePolicies.Add(exchange);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new SavedDto<DeliveryAndExchangeDto>(
            loaded.Draft.Version,
            new DeliveryAndExchangeDto([.. rules.Select(SettingsMapper.ToDto)], exchange is null ? null : SettingsMapper.ToDto(exchange)));
    }
}
