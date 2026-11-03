namespace Romp.Modules.Notifications.Application.OrderConfirmation;

/// <summary>
/// The customer-facing sentences that depend on the order, not on the template: what happens next (OCF-2) and the reason category of a
/// cancellation (OCF-4). Placeholder copy until the business supplies wording (Q-OCF-1). Reasons are categories only: no staff note, no detail.
/// </summary>
public static class OrderWording
{
    private const string CashOnDelivery = "COD";

    public static string NextStep(string paymentMethodCode, string? confirmUrl = null) =>
        paymentMethodCode == CashOnDelivery
            ? "Your order is pending confirmation. We will call you within about an hour to confirm it."
                + (string.IsNullOrWhiteSpace(confirmUrl) ? string.Empty : " To confirm it now, open: " + confirmUrl)
            : "Your payment has been received and we are preparing your order.";

    /// <summary>Order cancellation reason codes (Orders' CancelReasons) in words a customer can read; an unknown or missing code gets the plain fallback.</summary>
    public static string Reason(string? reasonCode) => reasonCode switch
    {
        "CODN" => "we could not reach you to confirm it",
        "CODR" => "you declined it when we called",
        "CUST" => "you asked us to cancel it",
        "PYFL" => "the payment did not go through",
        "PYEX" => "the payment was not completed in time",
        "STAF" => "we were unable to fulfil it",
        _ => "it could not be completed",
    };
}
