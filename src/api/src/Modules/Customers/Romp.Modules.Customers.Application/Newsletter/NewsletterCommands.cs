using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Newsletter;

namespace Romp.Modules.Customers.Application.Newsletter;

/// <summary>
/// Subscribes an email from the footer form (NLS-1, NLS-6). The answer is the same for a new, an already-subscribed and a previously
/// unsubscribed address, so it never shows whether an address is on the list. Only an unreadable address is refused.
/// </summary>
public sealed record SubscribeNewsletterCommand(string? Email, ConsentSource Source = ConsentSource.Footer) : IRequest, ICustomerCommand;

public sealed class SubscribeNewsletterCommandValidator : AbstractValidator<SubscribeNewsletterCommand>
{
    public SubscribeNewsletterCommandValidator()
    {
        RuleFor(c => c.Email).Must(e => NewsletterEmail.Normalize(e) is not null).WithMessage("That does not look like an email address.");
        RuleFor(c => c.Source).Must(s => s is ConsentSource.Footer).WithMessage("Not a public source.");
    }
}

public sealed class SubscribeNewsletterCommandHandler(NewsletterConsent consent) : IRequestHandler<SubscribeNewsletterCommand>
{
    public async Task Handle(SubscribeNewsletterCommand request, CancellationToken cancellationToken) =>
        await consent.SubscribeAsync(NewsletterEmail.Normalize(request.Email)!, request.Source, cancellationToken);
}

/// <summary>Result of following an unsubscribe link: the signature was valid or it was not. A valid link for a record that is gone is still "done" (NLS-3).</summary>
public sealed record UnsubscribeByTokenCommand(string? Token) : IRequest<bool>, ICustomerCommand;

public sealed class UnsubscribeByTokenCommandHandler(NewsletterConsent consent, IOptions<CustomersOptions> options)
    : IRequestHandler<UnsubscribeByTokenCommand, bool>
{
    public async Task<bool> Handle(UnsubscribeByTokenCommand request, CancellationToken cancellationToken)
    {
        var id = UnsubscribeToken.Verify(request.Token, options.Value.LinkSigningKey);
        if (id is null)
        {
            return false;
        }

        await consent.UnsubscribeByIdAsync(id.Value, ConsentSource.Link, cancellationToken);
        return true;
    }
}

/// <summary>Whether the signed-in customer's own email is on the list (NLS-2). The address is the customer's own, never a request value.</summary>
public sealed record MyNewsletter(bool Subscribed);

public sealed record GetMyNewsletterQuery : IRequest<MyNewsletter>;

public sealed class GetMyNewsletterQueryHandler(ICustomerDbContext dbContext, ICurrentCustomer current) : IRequestHandler<GetMyNewsletterQuery, MyNewsletter>
{
    public async Task<MyNewsletter> Handle(GetMyNewsletterQuery request, CancellationToken cancellationToken)
    {
        var customerId = current.Require();
        var email = await dbContext.Customers.AsNoTracking().Where(c => c.Id == customerId).Select(c => c.Email).SingleAsync(cancellationToken);
        var normalised = NewsletterEmail.Normalize(email);
        var subscribed = normalised is not null
            && await dbContext.Newsletter.AsNoTracking().AnyAsync(s => s.Email == normalised && s.Status == NewsletterStatus.Subscribed, cancellationToken);
        return new MyNewsletter(subscribed);
    }
}

/// <summary>Turns the signed-in customer's marketing email on or off from their account (NLS-2, source Account).</summary>
public sealed record SetMyNewsletterCommand(bool Subscribed) : IRequest<MyNewsletter>, ICustomerCommand;

public sealed class SetMyNewsletterCommandHandler(NewsletterConsent consent, ICustomerDbContext dbContext, ICurrentCustomer current)
    : IRequestHandler<SetMyNewsletterCommand, MyNewsletter>
{
    public async Task<MyNewsletter> Handle(SetMyNewsletterCommand request, CancellationToken cancellationToken)
    {
        var customerId = current.Require();
        var email = await dbContext.Customers.AsNoTracking().Where(c => c.Id == customerId).Select(c => c.Email).SingleAsync(cancellationToken);
        var normalised = NewsletterEmail.Normalize(email) ?? throw new InvalidOperationException("The account email is not a usable address.");

        if (request.Subscribed)
        {
            await consent.SubscribeAsync(normalised, ConsentSource.Account, cancellationToken);
        }
        else
        {
            await consent.UnsubscribeAsync(normalised, ConsentSource.Account, cancellationToken);
        }

        return new MyNewsletter(request.Subscribed);
    }
}
