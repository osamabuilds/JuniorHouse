namespace Romp.Modules.Customers.Application.Accounts;

/// <summary>
/// Checks the bot-challenge token (Cloudflare Turnstile) that sign-in, register and reset must carry once the throttle asks for it
/// (SEC-02, ACT-9). It fails closed: an unreachable service, an error or a missing token all mean "not verified".
/// </summary>
public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken);
}
