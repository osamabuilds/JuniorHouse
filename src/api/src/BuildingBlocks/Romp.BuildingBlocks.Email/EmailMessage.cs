namespace Romp.BuildingBlocks.Email;

/// <summary>
/// One outgoing email (ADR 0022). <c>Headers</c> carry things like <c>List-Unsubscribe</c> for marketing mail; transactional mail has none.
/// The body is built by the caller, which must HTML-encode every interpolated value.
/// </summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string TextBody,
    string? HtmlBody = null,
    IReadOnlyDictionary<string, string>? Headers = null);
