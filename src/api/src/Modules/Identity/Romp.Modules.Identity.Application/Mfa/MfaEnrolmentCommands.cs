using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Mfa;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Application.Mfa;

/// <summary>What a sealed authenticator secret is bound to, so a copy on another account's row does not open.</summary>
internal static class MfaBinding
{
    public static string For(long userId) => $"user:{userId}";
}

/// <summary>The issuer name an authenticator app shows beside the account.</summary>
public static class MfaIssuer
{
    public const string Name = "Romp Admin";
}

/// <summary>
/// The secret as it is shown once, during enrolment (AC-2): as the <c>otpauth://</c> address a QR code carries and as grouped text for
/// typing by hand. Both are <see cref="SecretString"/>s, so printing or serialising them cannot leak them (AC-7).
/// </summary>
public sealed record MfaEnrolmentStarted(SecretString OtpAuthUri, SecretString SecretText, DateTimeOffset ExpiresAt);

/// <summary>The recovery codes, returned once and never again (AC-6).</summary>
public sealed record RecoveryCodesIssued(IReadOnlyList<SecretString> Codes);

/// <summary>Whether the account has a second factor and how many recovery codes are left; never any secret.</summary>
public sealed record MfaStatus(bool Enrolled, int RecoveryCodesRemaining);

/// <summary>The real answer to "does this account have a second factor" that decides a session's scope (AC-1).</summary>
public sealed class MfaEnrolmentStatus(IIdentityDbContext dbContext) : IMfaEnrolmentStatus
{
    public Task<bool> IsEnrolledAsync(long userId, CancellationToken cancellationToken) =>
        dbContext.Staff.AsNoTracking().Where(u => u.Id == userId).Select(u => u.MfaEnrolledAt != null).FirstOrDefaultAsync(cancellationToken);
}

/// <summary>Forgets an account's second factor, its recovery codes and any unfinished enrolment, inside the caller's transaction (AC-8, AC-9).</summary>
public sealed class MfaEnrolmentReset(IIdentityDbContext dbContext) : IMfaEnrolmentReset
{
    public async Task ClearAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Staff.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        user?.ClearMfa();
        await dbContext.RecoveryCodes.Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.MfaEnrolmentsPending.Where(p => p.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }
}

/// <summary>The checks both enrolment steps share and the replacement of an account's recovery codes.</summary>
internal static class MfaEnrolmentSteps
{
    public static async Task<StaffUser> LoadActiveAsync(IIdentityDbContext dbContext, long userId, CancellationToken cancellationToken)
    {
        var user = await StaffLookups.GetAsync(dbContext, userId, cancellationToken);
        if (!user.IsActive)
        {
            throw new DomainException("This account is not active.");
        }

        return user;
    }

    /// <summary>Replaces every recovery code of the account with a new set, which is what invalidates the old ones (AC-6).</summary>
    public static async Task<RecoveryCodesIssued> ReplaceRecoveryCodesAsync(
        IIdentityDbContext dbContext, IRecoveryPepper pepper, long userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await dbContext.RecoveryCodes.Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        var (rows, codes) = RecoveryCodes.Issue(userId, pepper.Value, now);
        dbContext.RecoveryCodes.AddRange(rows);
        return new RecoveryCodesIssued(codes.Select(c => new SecretString(c)).ToList());
    }

    public static AbstractValidator<T> CodeRules<T>(System.Linq.Expressions.Expression<Func<T, string>> code)
    {
        var validator = new InlineValidator<T>();
        validator.RuleFor(code).NotEmpty().MaximumLength(16).Matches("^[0-9 ]{6,7}$").WithMessage("Enter the 6-digit code from your authenticator app.");
        return validator;
    }
}

/// <summary>
/// Step one of enrolment (AC-1, AC-2): makes a new secret, seals it, and keeps it as an unconfirmed enrolment for 15 minutes. Nothing
/// on the account changes until a valid code confirms it. Starting again replaces the unconfirmed secret.
/// </summary>
public sealed record StartMfaEnrolmentCommand(long UserId) : IRequest<MfaEnrolmentStarted>, IIdentityCommand;

public sealed class StartMfaEnrolmentCommandHandler(IIdentityDbContext dbContext, ISecretProtector protector, TimeProvider clock)
    : IRequestHandler<StartMfaEnrolmentCommand, MfaEnrolmentStarted>
{
    public async Task<MfaEnrolmentStarted> Handle(StartMfaEnrolmentCommand request, CancellationToken cancellationToken)
    {
        var user = await MfaEnrolmentSteps.LoadActiveAsync(dbContext, request.UserId, cancellationToken);
        if (user.IsMfaEnrolled)
        {
            throw new DomainException("Two-step verification is already set up for this account.");
        }

        if (user.MustChangePassword)
        {
            throw new DomainException("Choose your own password before setting up two-step verification.");
        }

        var now = clock.GetUtcNow();
        var secret = TotpSecret.Generate();
        var sealedSecret = protector.Protect(secret, MfaBinding.For(user.Id));

        await dbContext.MfaEnrolmentsPending.Where(p => p.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        var pending = MfaEnrolmentPending.Start(user.Id, sealedSecret.Cipher, sealedSecret.KeyId, now);
        dbContext.MfaEnrolmentsPending.Add(pending);

        var text = Base32.Encode(secret);
        var grouped = string.Join(' ', Enumerable.Range(0, text.Length / 4).Select(i => text.Substring(i * 4, 4)));
        return new MfaEnrolmentStarted(
            new SecretString(TotpSecret.OtpAuthUri(MfaIssuer.Name, user.UserName, secret)), new SecretString(grouped), pending.ExpiresAt);
    }
}

/// <summary>
/// Step two (AC-2, AC-6): a valid code from the new authenticator activates the secret and returns the recovery codes once. The step of
/// that code is recorded as used, so it cannot be replayed to sign in.
/// </summary>
public sealed record ConfirmMfaEnrolmentCommand(long UserId, string Code) : IRequest<RecoveryCodesIssued>, IIdentityCommand;

public sealed class ConfirmMfaEnrolmentCommandValidator : AbstractValidator<ConfirmMfaEnrolmentCommand>
{
    public ConfirmMfaEnrolmentCommandValidator() => Include(MfaEnrolmentSteps.CodeRules<ConfirmMfaEnrolmentCommand>(c => c.Code));
}

public sealed class ConfirmMfaEnrolmentCommandHandler(IIdentityDbContext dbContext, ISecretProtector protector, IRecoveryPepper pepper, TimeProvider clock)
    : IRequestHandler<ConfirmMfaEnrolmentCommand, RecoveryCodesIssued>
{
    public async Task<RecoveryCodesIssued> Handle(ConfirmMfaEnrolmentCommand request, CancellationToken cancellationToken)
    {
        var user = await MfaEnrolmentSteps.LoadActiveAsync(dbContext, request.UserId, cancellationToken);
        if (user.IsMfaEnrolled)
        {
            throw new DomainException("Two-step verification is already set up for this account.");
        }

        var now = clock.GetUtcNow();
        var pending = await dbContext.MfaEnrolmentsPending.FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);
        if (pending is null || pending.IsExpired(now))
        {
            throw new DomainException("There is no setup in progress. Start again to get a new code to scan.");
        }

        var secret = protector.Unprotect(new ProtectedSecret(pending.KeyId, pending.SecretCipher), MfaBinding.For(user.Id));
        var step = TotpVerifier.Verify(secret, request.Code, now, lastAcceptedStep: 0)
            ?? throw new DomainException("That code is not valid. Check the code in your app and try again.");

        // Sealed again under the current key, so a key rotated while the person was scanning is picked up at once.
        var sealedSecret = protector.Protect(secret, MfaBinding.For(user.Id));
        user.EnrolMfa(sealedSecret.Cipher, sealedSecret.KeyId, now, step);
        dbContext.MfaEnrolmentsPending.Remove(pending);
        return await MfaEnrolmentSteps.ReplaceRecoveryCodesAsync(dbContext, pepper, user.Id, now, cancellationToken);
    }
}

/// <summary>New recovery codes, which invalidate the old ones (AC-6). Needs a current authenticator code, so a stolen session alone cannot do it.</summary>
public sealed record RegenerateRecoveryCodesCommand(long UserId, string Code) : IRequest<RecoveryCodesIssued>, IIdentityCommand;

public sealed class RegenerateRecoveryCodesCommandValidator : AbstractValidator<RegenerateRecoveryCodesCommand>
{
    public RegenerateRecoveryCodesCommandValidator() => Include(MfaEnrolmentSteps.CodeRules<RegenerateRecoveryCodesCommand>(c => c.Code));
}

public sealed class RegenerateRecoveryCodesCommandHandler(IIdentityDbContext dbContext, ISecretProtector protector, IRecoveryPepper pepper, TimeProvider clock)
    : IRequestHandler<RegenerateRecoveryCodesCommand, RecoveryCodesIssued>
{
    public async Task<RecoveryCodesIssued> Handle(RegenerateRecoveryCodesCommand request, CancellationToken cancellationToken)
    {
        var user = await MfaEnrolmentSteps.LoadActiveAsync(dbContext, request.UserId, cancellationToken);
        if (!user.IsMfaEnrolled || user.MfaSecretCipher is null || user.MfaKeyId is null)
        {
            throw new DomainException("Two-step verification is not set up for this account.");
        }

        var now = clock.GetUtcNow();
        var secret = protector.Unprotect(new ProtectedSecret(user.MfaKeyId, user.MfaSecretCipher), MfaBinding.For(user.Id));
        var step = TotpVerifier.Verify(secret, request.Code, now, user.MfaLastStep)
            ?? throw new DomainException("That code is not valid. Wait for the next code in your app and try again.");

        user.TryAcceptMfaStep(step);
        return await MfaEnrolmentSteps.ReplaceRecoveryCodesAsync(dbContext, pepper, user.Id, now, cancellationToken);
    }
}

public sealed record GetMfaStatusQuery(long UserId) : IRequest<MfaStatus>;

public sealed class GetMfaStatusQueryHandler(IIdentityDbContext dbContext) : IRequestHandler<GetMfaStatusQuery, MfaStatus>
{
    public async Task<MfaStatus> Handle(GetMfaStatusQuery request, CancellationToken cancellationToken)
    {
        var enrolled = await dbContext.Staff.AsNoTracking().Where(u => u.Id == request.UserId).Select(u => u.MfaEnrolledAt != null).FirstOrDefaultAsync(cancellationToken);
        var remaining = enrolled
            ? await dbContext.RecoveryCodes.AsNoTracking().CountAsync(c => c.UserId == request.UserId && c.UsedAt == null, cancellationToken)
            : 0;
        return new MfaStatus(enrolled, remaining);
    }
}
