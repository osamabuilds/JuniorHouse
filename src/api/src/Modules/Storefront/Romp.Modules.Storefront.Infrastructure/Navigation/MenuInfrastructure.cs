using Microsoft.Extensions.Options;
using Romp.Modules.Storefront.Application.Navigation;

namespace Romp.Modules.Storefront.Infrastructure.Navigation;

public sealed class MenuSettings(IOptions<StorefrontOptions> options) : IMenuSettings
{
    public IReadOnlySet<string> HiddenGenderCodes { get; } =
        options.Value.MenuHiddenGenderCodes.Select(c => c.Trim().ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);
}
