using System.Security.Cryptography;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Infrastructure.StaffAccounts;

/// <summary>
/// 16 characters from a cryptographic RNG, drawn from letters and digits with the look-alikes removed
/// (0 O 1 l I 5 S 2 Z 8 B), so it can be read out or typed without mistakes. Always has a lower-case letter,
/// an upper-case letter and a digit.
/// </summary>
public sealed class TemporaryPasswordGenerator : ITemporaryPasswordGenerator
{
    private const int Length = 16;
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Upper = "ACDEFGHJKLMNPQRTUVWXY";
    private const string Digits = "34679";
    private const string All = Lower + Upper + Digits;

    public SecretString Generate()
    {
        var chars = new char[Length];
        chars[0] = Pick(Lower);
        chars[1] = Pick(Upper);
        chars[2] = Pick(Digits);
        for (var i = 3; i < Length; i++)
        {
            chars[i] = Pick(All);
        }

        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new SecretString(new string(chars));
    }

    private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
