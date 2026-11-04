using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.NotifyMe;
using Romp.Modules.Storefront.Contracts;

namespace Romp.Modules.Customers.Application.NotifyMe;

/// <summary>
/// Asks to be told when a sold-out variant is back (FR-11, NTM-2, NTM-3, NTM-6). The answer is the same for a new request and for a
/// repeat of an open one, so it never shows whether an address is already waiting. A variant that does not exist or is in stock records
/// nothing and says what is wrong.
/// </summary>
public sealed record CreateNotifyRequestCommand(long VariantId, string? Email) : IRequest, ICustomerCommand;

public sealed class CreateNotifyRequestCommandValidator : AbstractValidator<CreateNotifyRequestCommand>
{
    public CreateNotifyRequestCommandValidator()
    {
        RuleFor(c => c.VariantId).GreaterThan(0).WithMessage("Choose a size first.");
        RuleFor(c => c.Email).Must(e => NotifyEmail.Normalize(e) is not null).WithMessage("That does not look like an email address.");
    }
}

public sealed class CreateNotifyRequestCommandHandler(
    ICustomerDbContext dbContext, IStorefrontCatalogQueries catalog, IOptions<CustomersOptions> options, TimeProvider clock)
    : IRequestHandler<CreateNotifyRequestCommand>
{
    public async Task Handle(CreateNotifyRequestCommand request, CancellationToken cancellationToken)
    {
        var email = NotifyEmail.Normalize(request.Email)!;

        // The storefront read model is what the shopper saw (Q-NTM-4): the variant must be known there and sold out.
        var variants = await catalog.GetVariantSummariesAsync([request.VariantId], cancellationToken);
        var variant = variants.Count > 0 ? variants[0] : null;
        if (variant is null)
        {
            throw Refuse("That size is not available to request.");
        }

        if (variant.IsAvailable)
        {
            throw Refuse("That size is in stock, so you can add it to your bag.");
        }

        // One request at a time per variant and address, so a double click cannot make two; the partial unique index is only the backstop.
        await dbContext.LockKeyAsync($"notify:{request.VariantId}:{email}", cancellationToken);
        var open = NotifyStatus.Open;
        if (await dbContext.NotifyRequests.AnyAsync(r => r.VariantId == request.VariantId && r.Email == email && r.Status == open, cancellationToken))
        {
            return;
        }

        var waiting = await dbContext.NotifyRequests.CountAsync(r => r.VariantId == request.VariantId && r.Status == open, cancellationToken);
        if (!RestockRule.HasRoom(waiting, options.Value.MaxNotifyPerVariant))
        {
            throw Refuse("Too many people are already waiting for this size. Please try again later.");
        }

        dbContext.NotifyRequests.Add(NotifyRequest.Open(request.VariantId, email, clock.GetUtcNow()));
    }

    private static Romp.BuildingBlocks.Application.ValidationException Refuse(string message) =>
        new(new Dictionary<string, string[]> { [nameof(CreateNotifyRequestCommand.VariantId)] = [message] });
}
