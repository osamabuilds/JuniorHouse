using Microsoft.Extensions.Options;

namespace Romp.BuildingBlocks.Email;

/// <summary><c>Email:Smtp:*</c>. The host comes from configuration only, never from a request (ADR 0022, SSRF). The password is a secret.</summary>
public sealed class SmtpEmailOptions
{
    public const string Section = "Email:Smtp";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 1025;

    public bool UseStartTls { get; set; }

    public string? User { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Romp";
}

public sealed class SmtpEmailOptionsValidator : IValidateOptions<SmtpEmailOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpEmailOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            return ValidateOptionsResult.Fail("Email:Smtp:Host is required.");
        }

        if (options.Port is < 1 or > 65535)
        {
            return ValidateOptionsResult.Fail("Email:Smtp:Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress) || !options.FromAddress.Contains('@', StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail("Email:Smtp:FromAddress must be an email address.");
        }

        return ValidateOptionsResult.Success;
    }
}
