using Romp.Modules.Customers.Contracts;
using Microsoft.Extensions.Logging;
using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Application.Accounts;

/// <summary>
/// Sends the two emails that carry a one-time link (ACT-6, ACT-27). The implementation builds the link from configuration and renders the
/// message (Infrastructure). The token is a <see cref="SecretString"/>: it prints as a mask and must never be logged.
/// </summary>
public interface ICustomerMailer
{
    Task SendVerificationAsync(string toEmail, string firstName, SecretString token, CancellationToken cancellationToken);

    Task SendPasswordResetAsync(string toEmail, string firstName, SecretString token, CancellationToken cancellationToken);
}


internal static partial class AccountLog
{
    [LoggerMessage(EventId = 2101, Level = LogLevel.Error, Message = "Account email not sent: kind={Kind}. The request itself succeeded.")]
    public static partial void MailNotSent(this ILogger logger, string kind, Exception exception);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Error, Message = "A sign-in hook failed for customer {CustomerId}. The sign-in itself succeeded.")]
    public static partial void HookFailed(this ILogger logger, long customerId, Exception exception);
}

/// <summary>
/// Sends account email without ever failing the request that asked for it (NFR-FT-08): a failure is logged without the address or the link
/// and the customer can ask again. Guaranteed delivery with retry arrives with the Notifications module (Sprint 13).
/// </summary>
public sealed class AccountMail(ICustomerMailer mailer, ILoggerFactory loggers)
{
    private readonly ILogger _logger = loggers.CreateLogger<AccountMail>();

    public async Task TrySendVerificationAsync(string toEmail, string firstName, SecretString token, CancellationToken cancellationToken)
    {
        try
        {
            await mailer.SendVerificationAsync(toEmail, firstName, token, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.MailNotSent("verification", ex);
        }
    }

    public async Task TrySendPasswordResetAsync(string toEmail, string firstName, SecretString token, CancellationToken cancellationToken)
    {
        try
        {
            await mailer.SendPasswordResetAsync(toEmail, firstName, token, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.MailNotSent("password reset", ex);
        }
    }
}

/// <summary>Runs every registered <see cref="ICustomerSignedInHook"/>; one that throws is logged and skipped.</summary>
public sealed class SignedInHooks(IEnumerable<ICustomerSignedInHook> hooks, ILoggerFactory loggers)
{
    private readonly ILogger _logger = loggers.CreateLogger<SignedInHooks>();

    public async Task RunAsync(long customerId, string? guestSessionId, CancellationToken cancellationToken)
    {
        var context = new SignedInContext(customerId, guestSessionId);
        foreach (var hook in hooks)
        {
            try
            {
                await hook.OnSignedInAsync(context, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.HookFailed(customerId, ex);
            }
        }
    }
}
