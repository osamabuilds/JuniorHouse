using Microsoft.Extensions.Logging;

namespace Romp.BuildingBlocks.Application;

/// <summary>
/// Security events (observability AC-9): one fixed event id and one outcome code per kind of event, under the dedicated category
/// <see cref="Category"/>, so an alert or a search can rely on them never changing. Every value is typed, never free text, and none
/// is a secret: no password, token, hash or typed user name (a person can type their password into the name box).
/// </summary>
public static partial class SecurityEvents
{
    public const string Category = "Romp.Security";

    public const int SignInFailedId = 1001;
    public const int MfaFailedId = 1002;
    public const int RefreshReuseId = 1003;
    public const int RateLimitedId = 1004;
    public const int ForbiddenId = 1005;
    public const int UnauthenticatedId = 1006;
    public const int SessionRevokedByAdminId = 1007;

    /// <summary>A password step that did not succeed. <paramref name="outcome"/> is the lookup code (bad_credentials, inactive, temporary_expired).</summary>
    [LoggerMessage(EventId = SignInFailedId, Level = LogLevel.Warning, Message = "Security event: sign-in failed. Outcome={Outcome} UserId={UserId} ClientIp={ClientIp}")]
    public static partial void SignInFailed(this ILogger logger, string outcome, long userId, string clientIp);

    /// <summary>A wrong second-step code (outcome mfa_bad_code), or the fifth, which ended the pending sign-in (mfa_pending_ended).</summary>
    [LoggerMessage(EventId = MfaFailedId, Level = LogLevel.Warning, Message = "Security event: second step failed. Outcome={Outcome} UserId={UserId} ClientIp={ClientIp}")]
    public static partial void MfaFailed(this ILogger logger, string outcome, long userId, string clientIp);

    /// <summary>A sign-in attempt refused because a delay is running for the name or the address (outcome signin_throttled).</summary>
    [LoggerMessage(EventId = RateLimitedId, Level = LogLevel.Warning, Message = "Security event: sign-in throttled. Outcome=signin_throttled RetryAfterSeconds={RetryAfterSeconds} ClientIp={ClientIp}")]
    public static partial void SignInThrottled(this ILogger logger, int retryAfterSeconds, string clientIp);

    /// <summary>An already rotated refresh value was presented after the grace; the session is revoked (outcome refresh_reuse).</summary>
    [LoggerMessage(EventId = RefreshReuseId, Level = LogLevel.Warning, Message = "Security event: refresh value reused. Outcome=refresh_reuse SessionId={SessionId} UserId={UserId}")]
    public static partial void RefreshReuse(this ILogger logger, long sessionId, long userId);
}
