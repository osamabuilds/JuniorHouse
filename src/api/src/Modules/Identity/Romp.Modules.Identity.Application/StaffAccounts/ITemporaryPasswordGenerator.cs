using Romp.Modules.Identity.Domain.StaffAccounts;

namespace Romp.Modules.Identity.Application.StaffAccounts;

/// <summary>Makes the one-time password an Owner hands to a new or reset staff member (staff-accounts AC-4).</summary>
public interface ITemporaryPasswordGenerator
{
    SecretString Generate();
}
