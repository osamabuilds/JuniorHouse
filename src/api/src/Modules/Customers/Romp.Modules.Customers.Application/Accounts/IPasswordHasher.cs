using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Application.Accounts;

/// <summary>One-way, salted password hashing (ACT-4). The hash is a self-describing string. Written in Customers, not shared with Identity (ADR 0023).</summary>
public interface IPasswordHasher
{
    string Hash(SecretString password);

    /// <summary>False for a wrong password and for a stored value that is not a hash this hasher understands.</summary>
    bool Verify(SecretString password, string storedHash);

    /// <summary>True when the stored hash used weaker parameters than the current ones, so it can be replaced at the next sign-in.</summary>
    bool NeedsRehash(string storedHash);
}
