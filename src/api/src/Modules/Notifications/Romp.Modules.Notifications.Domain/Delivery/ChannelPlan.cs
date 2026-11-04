namespace Romp.Modules.Notifications.Domain.Delivery;

/// <summary>Which channels one notification is created for.</summary>
public sealed record ChannelPlanResult(bool Email, bool WhatsApp, ErrorCategory? WhatsAppSkipReason);

/// <summary>
/// The rule for choosing channels (pure). Email is always planned. WhatsApp only when the customer opted in and the number has not said
/// STOP (CHN-1, CHN-2, CHN-6). A WhatsApp problem never removes the email (CHN-3, FR-64).
/// </summary>
public static class ChannelPlan
{
    public static ChannelPlanResult For(bool whatsAppOptIn, bool numberSuppressed) =>
        whatsAppOptIn && !numberSuppressed
            ? new ChannelPlanResult(true, true, null)
            : new ChannelPlanResult(true, false, ErrorCategory.OptedOut);
}
