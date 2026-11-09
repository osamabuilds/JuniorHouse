using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Application.Accounts;

public enum SendVerificationStatus
{
    Sent = 1,

    /// <summary>The email is already verified, so nothing is sent (ACT-31).</summary>
    AlreadyVerified = 2,

    /// <summary>The hourly limit for new links was reached (ACT-28).</summary>
    RateLimited = 3,
}

/// <summary>Sends the signed-in customer a fresh verification link; the previous link stops working (ACT-28, ACT-31).</summary>
public sealed record SendEmailVerificationCommand : IRequest<SendVerificationStatus>, ICustomerCommand;

/// <summary>The one place a verification link is issued, so registration, resend and change of email follow the same rules.</summary>
public sealed class EmailVerificationIssuer(ICustomerDbContext dbContext, AccountMail mail, TimeProvider clock)
{
    public async Task<SendVerificationStatus> IssueAndSendAsync(Customer customer, CancellationToken cancellationToken)
    {
        if (customer.EmailIsVerified)
        {
            return SendVerificationStatus.AlreadyVerified;
        }

        var now = clock.GetUtcNow();
        var since = now - TimeSpan.FromHours(1);
        var recent = await dbContext.EmailVerificationTokens.AsNoTracking()
            .CountAsync(t => t.CustomerId == customer.Id && t.IssuedAt >= since, cancellationToken);
        if (recent >= EmailVerificationToken.MaxPerHour)
        {
            return SendVerificationStatus.RateLimited;
        }

        var older = await dbContext.EmailVerificationTokens.Where(t => t.CustomerId == customer.Id && t.UsedAt == null).ToListAsync(cancellationToken);
        foreach (var token in older)
        {
            token.Supersede(now);
        }

        var (fresh, secret) = EmailVerificationToken.Issue(customer.Id, customer.Email, now);
        dbContext.EmailVerificationTokens.Add(fresh);
        await dbContext.SaveChangesAsync(cancellationToken);

        await mail.TrySendVerificationAsync(customer.Email, customer.FirstName, secret, cancellationToken);
        return SendVerificationStatus.Sent;
    }
}

public sealed class SendEmailVerificationCommandHandler(ICustomerDbContext dbContext, ICurrentCustomer current, EmailVerificationIssuer issuer)
    : IRequestHandler<SendEmailVerificationCommand, SendVerificationStatus>
{
    public async Task<SendVerificationStatus> Handle(SendEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        var customerId = current.CustomerId ?? throw new UnauthorizedAccessException("A signed-in customer is required.");
        var customer = await dbContext.Customers.SingleAsync(c => c.Id == customerId, cancellationToken);
        return await issuer.IssueAndSendAsync(customer, cancellationToken);
    }
}

public enum VerifyEmailResult
{
    Verified = 1,

    /// <summary>Unknown, used, expired or replaced; one answer for all of them (ACT-27).</summary>
    Invalid = 2,
}

/// <summary>Uses the emailed link: the email on the account becomes verified. Anonymous: the link itself is the proof (ACT-27).</summary>
public sealed record VerifyEmailCommand(string Token) : IRequest<VerifyEmailResult>, ICustomerCommand;

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(c => c.Token).NotNull();
    }
}

public sealed class VerifyEmailCommandHandler(ICustomerDbContext dbContext, TimeProvider clock) : IRequestHandler<VerifyEmailCommand, VerifyEmailResult>
{
    public async Task<VerifyEmailResult> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Token) || request.Token.Length > CustomerSessionIssuer.MaxPresentedLength)
        {
            return VerifyEmailResult.Invalid;
        }

        var now = clock.GetUtcNow();
        var hash = CustomerTokens.Hash(request.Token);
        var token = await dbContext.EmailVerificationTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null || !token.Consume(request.Token, now))
        {
            return VerifyEmailResult.Invalid;
        }

        var customer = await dbContext.Customers.SingleAsync(c => c.Id == token.CustomerId, cancellationToken);

        // A link for an address the account no longer has proves nothing about the address it has now.
        return customer.MarkEmailVerified(token.Email, now) ? VerifyEmailResult.Verified : VerifyEmailResult.Invalid;
    }
}

public enum ChangeEmailStatus
{
    Changed = 1,
    WrongPassword = 2,
    EmailTaken = 3,
}

/// <summary>Changes the sign-in email; the new address starts unverified and gets its own link (ACT-28). Needs the current password.</summary>
public sealed record ChangeEmailCommand(string NewEmail, SecretString CurrentPassword) : IRequest<ChangeEmailStatus>, ICustomerCommand;

public sealed class ChangeEmailCommandValidator : AbstractValidator<ChangeEmailCommand>
{
    public ChangeEmailCommandValidator()
    {
        RuleFor(c => c.NewEmail).NotEmpty().MaximumLength(Customer.EmailMaxLength)
            .Must(email =>
            {
                try
                {
                    _ = Customer.NormaliseEmail(email);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }).WithMessage("That does not look like an email address.");
        RuleFor(c => c.CurrentPassword).NotNull();
    }
}

public sealed class ChangeEmailCommandHandler(
    ICustomerDbContext dbContext, ICurrentCustomer current, IPasswordHasher hasher, EmailVerificationIssuer issuer, TimeProvider clock)
    : IRequestHandler<ChangeEmailCommand, ChangeEmailStatus>
{
    public async Task<ChangeEmailStatus> Handle(ChangeEmailCommand request, CancellationToken cancellationToken)
    {
        var customerId = current.CustomerId ?? throw new UnauthorizedAccessException("A signed-in customer is required.");
        var customer = await dbContext.Customers.SingleAsync(c => c.Id == customerId, cancellationToken);

        if (!customer.HasPassword || !hasher.Verify(request.CurrentPassword, customer.PasswordHash!))
        {
            return ChangeEmailStatus.WrongPassword;
        }

        var email = Customer.NormaliseEmail(request.NewEmail);
        if (await dbContext.Customers.AnyAsync(c => c.Email == email && c.Id != customerId, cancellationToken))
        {
            return ChangeEmailStatus.EmailTaken;
        }

        customer.ChangeEmail(email);

        // Links sent to the old address are retired; the new address gets its own.
        var now = clock.GetUtcNow();
        var older = await dbContext.EmailVerificationTokens.Where(t => t.CustomerId == customerId && t.UsedAt == null).ToListAsync(cancellationToken);
        foreach (var token in older)
        {
            token.Supersede(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await issuer.IssueAndSendAsync(customer, cancellationToken);
        return ChangeEmailStatus.Changed;
    }
}

/// <summary>The same answer for every request: nothing says whether the address has an account (ACT-6).</summary>
public enum PasswordResetRequested
{
    Accepted = 1,
}

/// <summary>Emails a reset link when the address belongs to an account with a password. Anonymous.</summary>
public sealed record RequestPasswordResetCommand(string Email) : IRequest<PasswordResetRequested>, ICustomerCommand;

public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        // A bad address is not a different answer: it gets the same "if an account exists" reply (ACT-6).
        RuleFor(c => c.Email).NotNull();
    }
}

public sealed class RequestPasswordResetCommandHandler(ICustomerDbContext dbContext, AccountMail mail, TimeProvider clock)
    : IRequestHandler<RequestPasswordResetCommand, PasswordResetRequested>
{
    public async Task<PasswordResetRequested> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        string email;
        try
        {
            email = Customer.NormaliseEmail(request.Email);
        }
        catch (ArgumentException)
        {
            return PasswordResetRequested.Accepted;
        }

        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.Email == email, cancellationToken);
        if (customer is null || !customer.HasPassword)
        {
            return PasswordResetRequested.Accepted;
        }

        var now = clock.GetUtcNow();
        var older = await dbContext.PasswordResetTokens.Where(t => t.CustomerId == customer.Id && t.UsedAt == null).ToListAsync(cancellationToken);
        foreach (var token in older)
        {
            token.Supersede(now);
        }

        var (fresh, secret) = PasswordResetToken.Issue(customer.Id, now);
        dbContext.PasswordResetTokens.Add(fresh);
        await dbContext.SaveChangesAsync(cancellationToken);

        await mail.TrySendPasswordResetAsync(customer.Email, customer.FirstName, secret, cancellationToken);
        return PasswordResetRequested.Accepted;
    }
}

public enum ResetPasswordStatus
{
    Reset = 1,

    /// <summary>Unknown, used, expired or replaced; one answer for all of them (ACT-8).</summary>
    Invalid = 2,

    PasswordRejected = 3,
}

public sealed record ResetPasswordResult(ResetPasswordStatus Status, IReadOnlyList<string> UnmetRules)
{
    public static ResetPasswordResult Done { get; } = new(ResetPasswordStatus.Reset, []);

    public static ResetPasswordResult NotValid { get; } = new(ResetPasswordStatus.Invalid, []);
}

/// <summary>Sets a new password with the emailed link; every session of the customer ends (ACT-7, ACT-13). Anonymous.</summary>
public sealed record ResetPasswordCommand(string Token, SecretString NewPassword) : IRequest<ResetPasswordResult>, ICustomerCommand;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(c => c.Token).NotNull();
        RuleFor(c => c.NewPassword).NotNull();
    }
}

public sealed class ResetPasswordCommandHandler(ICustomerDbContext dbContext, IPasswordHasher hasher, CustomerSessionIssuer sessions, TimeProvider clock)
    : IRequestHandler<ResetPasswordCommand, ResetPasswordResult>
{
    public async Task<ResetPasswordResult> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Token) || request.Token.Length > CustomerSessionIssuer.MaxPresentedLength)
        {
            return ResetPasswordResult.NotValid;
        }

        var now = clock.GetUtcNow();
        var hash = CustomerTokens.Hash(request.Token);
        var token = await dbContext.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null || !token.IsUsable(now) || !CustomerTokens.Matches(request.Token, token.TokenHash))
        {
            return ResetPasswordResult.NotValid;
        }

        // A weak password is checked before the link is used up, so the customer can try again with the same link.
        var unmet = PasswordRule.Check(request.NewPassword.Reveal()).Where(c => !c.Met).Select(c => c.Code).ToList();
        if (unmet.Count > 0)
        {
            return new ResetPasswordResult(ResetPasswordStatus.PasswordRejected, unmet);
        }

        token.Consume(request.Token, now);
        var customer = await dbContext.Customers.SingleAsync(c => c.Id == token.CustomerId, cancellationToken);
        customer.SetPasswordHash(hasher.Hash(request.NewPassword));
        await sessions.RevokeAllAsync(customer.Id, exceptSessionId: null, CustomerSessionRevokeReason.PasswordChanged, cancellationToken);
        return ResetPasswordResult.Done;
    }
}

public enum ChangePasswordStatus
{
    Changed = 1,
    WrongPassword = 2,
    PasswordRejected = 3,
}

/// <summary>
/// Changes the password of the signed-in customer. Needs the current one; every other session ends, the one that asked stays (ACT-13).
/// <paramref name="PresentedAccessToken"/> is the session cookie that came with the request.
/// </summary>
public sealed record ChangePasswordCommand(SecretString CurrentPassword, SecretString NewPassword, string? PresentedAccessToken)
    : IRequest<ChangePasswordStatus>, ICustomerCommand;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(c => c.CurrentPassword).NotNull();
        RuleFor(c => c.NewPassword).NotNull();
    }
}

public sealed class ChangePasswordCommandHandler(
    ICustomerDbContext dbContext, ICurrentCustomer current, IPasswordHasher hasher, CustomerSessionIssuer sessions)
    : IRequestHandler<ChangePasswordCommand, ChangePasswordStatus>
{
    public async Task<ChangePasswordStatus> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var customerId = current.CustomerId ?? throw new UnauthorizedAccessException("A signed-in customer is required.");
        var customer = await dbContext.Customers.SingleAsync(c => c.Id == customerId, cancellationToken);

        if (!customer.HasPassword || !hasher.Verify(request.CurrentPassword, customer.PasswordHash!))
        {
            return ChangePasswordStatus.WrongPassword;
        }

        if (!PasswordRule.IsValid(request.NewPassword.Reveal()))
        {
            return ChangePasswordStatus.PasswordRejected;
        }

        customer.SetPasswordHash(hasher.Hash(request.NewPassword));
        var keep = await sessions.FindAccessSessionIdAsync(customerId, request.PresentedAccessToken, cancellationToken);
        await sessions.RevokeAllAsync(customerId, keep, CustomerSessionRevokeReason.PasswordChanged, cancellationToken);
        return ChangePasswordStatus.Changed;
    }
}
