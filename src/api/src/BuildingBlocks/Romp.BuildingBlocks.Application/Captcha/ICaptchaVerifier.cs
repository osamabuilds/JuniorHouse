namespace Romp.BuildingBlocks.Application.Captcha;

/// <summary>
/// Checks the bot-challenge token (Cloudflare Turnstile) that public endpoints must carry once a throttle asks for it (customer sign-in,
/// register and reset: SEC-02, ACT-9; order look-up: SEC-06). It fails closed: an unreachable service, an error or a missing token all
/// mean "not verified". Shared by every module that needs it, so none reaches into another module's internals (ADR 0024).
/// </summary>
public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken);
}
