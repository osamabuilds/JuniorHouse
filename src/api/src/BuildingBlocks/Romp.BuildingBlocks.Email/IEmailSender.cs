namespace Romp.BuildingBlocks.Email;

/// <summary>
/// The port every module sends email through (ADR 0022). A failure throws, so the outbox dispatcher retries; callers write the intent
/// through the outbox and never call this inside a business transaction.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
