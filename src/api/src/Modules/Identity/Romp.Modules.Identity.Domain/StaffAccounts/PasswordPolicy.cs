using System.Collections.Immutable;

namespace Romp.Modules.Identity.Domain.StaffAccounts;

/// <summary>Why a password was refused (staff-accounts AC-2).</summary>
public enum PasswordPolicyFailure
{
    TooShort,
    TooLong,
    EqualsUserName,
    Common,
}

/// <summary>The outcome of checking one password; every failing rule is listed, not just the first.</summary>
public sealed record PasswordPolicyResult(ImmutableArray<PasswordPolicyFailure> Failures)
{
    public bool IsAcceptable => Failures.IsEmpty;
}

/// <summary>The words shown to the person who chose the password. They say why, and never echo the password.</summary>
public static class PasswordPolicyMessages
{
    public static string For(PasswordPolicyFailure failure) => failure switch
    {
        PasswordPolicyFailure.TooShort => $"The password must be at least {PasswordPolicy.MinLength} characters.",
        PasswordPolicyFailure.TooLong => $"The password must be at most {PasswordPolicy.MaxLength} characters.",
        PasswordPolicyFailure.EqualsUserName => "The password must not be the same as the user name.",
        PasswordPolicyFailure.Common => "That password is on the list of commonly used passwords. Choose another.",
        _ => "The password is not acceptable.",
    };
}

/// <summary>A list of passwords that must be refused whatever their length.</summary>
public interface ICommonPasswords
{
    bool Contains(string password);
}

/// <summary>Length 12 to 128, not the user name, not on the common list (staff-accounts AC-2, NFR-13).</summary>
public sealed class PasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    private readonly ICommonPasswords _common;

    public PasswordPolicy(ICommonPasswords common)
    {
        _common = common;
    }

    public PasswordPolicyResult Evaluate(string password, string userName)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(userName);

        var failures = ImmutableArray.CreateBuilder<PasswordPolicyFailure>();

        if (password.Length < MinLength)
        {
            failures.Add(PasswordPolicyFailure.TooShort);
        }

        if (password.Length > MaxLength)
        {
            failures.Add(PasswordPolicyFailure.TooLong);
        }

        if (string.Equals(password, userName, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(PasswordPolicyFailure.EqualsUserName);
        }

        if (_common.Contains(password))
        {
            failures.Add(PasswordPolicyFailure.Common);
        }

        return new PasswordPolicyResult(failures.ToImmutable());
    }
}
