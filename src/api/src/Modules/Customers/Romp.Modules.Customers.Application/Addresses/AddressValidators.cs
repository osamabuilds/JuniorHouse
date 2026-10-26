using FluentValidation;
using Romp.Modules.Customers.Domain.Addresses;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Customers.Application.Addresses;

/// <summary>The rules every address shares, each message saying what to do (ADD-4, ADD-5, ADD-8). The domain checks the same limits again.</summary>
public abstract class AddressInputValidator<T> : AbstractValidator<T>
    where T : IAddressInput
{
    protected AddressInputValidator(IGeographyQueries geography)
    {
        RuleFor(x => x.RecipientName).Must(v => InRange(v, CustomerAddress.RecipientMinLength, CustomerAddress.RecipientMaxLength))
            .WithMessage($"Enter the recipient's name ({CustomerAddress.RecipientMinLength} to {CustomerAddress.RecipientMaxLength} characters).");
        RuleFor(x => x.Phone).Must(v => PhoneNumber.TryNormalise(v, out _))
            .WithMessage("Enter a Pakistani phone number such as 0300 1234567.");
        RuleFor(x => x.LineOne).Must(v => InRange(v, CustomerAddress.LineOneMinLength, CustomerAddress.LineMaxLength))
            .WithMessage($"Enter the street address ({CustomerAddress.LineOneMinLength} to {CustomerAddress.LineMaxLength} characters).");
        RuleFor(x => x.LineTwo).Must(v => string.IsNullOrWhiteSpace(v) || InRange(v, 1, CustomerAddress.LineMaxLength))
            .WithMessage($"Address line 2 can be at most {CustomerAddress.LineMaxLength} characters.");
        RuleFor(x => x.Postcode).Must(v => string.IsNullOrWhiteSpace(v) || (v.Trim().Length <= CustomerAddress.PostcodeMaxLength && v.Trim().All(char.IsAsciiLetterOrDigit)))
            .WithMessage($"The postcode can have letters and digits only, up to {CustomerAddress.PostcodeMaxLength} characters.");
        RuleFor(x => x.ProvinceId).GreaterThan((short)0).WithMessage("Choose a province.");
        RuleFor(x => x.CityId).GreaterThan((short)0).WithMessage("Choose a city.");

        // One call to Reference, and only once the cheaper checks on the two ids have passed.
        RuleFor(x => x.CityId)
            .MustAsync((command, cityId, token) => geography.CityBelongsToProvinceAsync(cityId, command.ProvinceId, token))
            .When(x => x.CityId > 0 && x.ProvinceId > 0)
            .WithMessage("Choose a city that is in the selected province.");
    }

    private static bool InRange(string? value, int min, int max)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length >= min && trimmed.Length <= max && !trimmed.Any(char.IsControl);
    }
}

public sealed class AddAddressCommandValidator(IGeographyQueries geography) : AddressInputValidator<AddAddressCommand>(geography);

public sealed class UpdateAddressCommandValidator(IGeographyQueries geography) : AddressInputValidator<UpdateAddressCommand>(geography);
