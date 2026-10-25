using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Identity.Domain.StaffAccounts;

/// <summary>A sign-in name: 3 to 40 characters of <c>a-z 0-9 . _ -</c>, stored lower-case (staff-accounts AC-10).</summary>
public readonly record struct UserName
{
    public const int MinLength = 3;
    public const int MaxLength = 40;

    private UserName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static UserName Create(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var value = raw.Trim().ToLowerInvariant();
        if (value.Length is < MinLength or > MaxLength || !value.All(IsAllowed))
        {
            throw new DomainException(
                $"A user name is {MinLength} to {MaxLength} characters of lower-case letters, digits, '.', '_' and '-'.");
        }

        return new UserName(value);
    }

    public override string ToString() => Value;

    private static bool IsAllowed(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or '-';
}
