namespace Romp.Modules.Customers.Domain.Accounts;

/// <summary>One line of the password checklist: a stable code, the words shown to the customer, and whether the typed password meets it.</summary>
public sealed record PasswordCheck(string Code, string Message, bool Met);

/// <summary>
/// The customer password rule (ACT-2, Q-ACT-3): 8 to 128 characters with a capital letter, a small letter and a digit; spaces are not allowed
/// and special characters are. <see cref="Check"/> lists every rule in a fixed order so the browser can show each as met or not met while the
/// customer types, and the server returns the same codes when it refuses a password.
/// </summary>
public static class PasswordRule
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public const string Length = "LENGTH";
    public const string Capital = "CAPITAL";
    public const string Small = "SMALL";
    public const string Digit = "DIGIT";
    public const string NoSpaces = "NO_SPACES";

    public static IReadOnlyList<PasswordCheck> Check(string? password)
    {
        var text = password ?? string.Empty;
        return
        [
            new(Length, $"{MinLength} to {MaxLength} characters", text.Length is >= MinLength and <= MaxLength),
            new(Capital, "A capital letter", text.Any(char.IsUpper)),
            new(Small, "A small letter", text.Any(char.IsLower)),
            new(Digit, "A number", text.Any(char.IsDigit)),
            new(NoSpaces, "No spaces", !text.Any(char.IsWhiteSpace)),
        ];
    }

    public static bool IsValid(string? password) => Check(password).All(c => c.Met);
}
