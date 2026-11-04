using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.Modules.Orders.Domain.Orders;

namespace Romp.Modules.Orders.Infrastructure.Persistence;

/// <summary>
/// The system-owned Orders lookups (docs/db/naming.md standard lookup shape), seeded by migration. The codes are the stable keys that code
/// and the specs refer to; ids are never reused.
/// </summary>
internal static class OrdersLookupConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        Configure<OrderStatusLookup>(modelBuilder, "ORDR_STS_LKP",
            (OrderStatuses.PendingConfirmation, "PNDC", "Pending Confirmation", "Cash on delivery: waiting for the customer to be confirmed"),
            (OrderStatuses.Confirmed, "CNFD", "Confirmed", "Confirmed and ready to be fulfilled"),
            (OrderStatuses.PaymentPending, "PYPN", "Payment Pending", "Online payment has not completed yet"),
            (OrderStatuses.Paid, "PAID", "Paid", "Paid online"),
            (OrderStatuses.Cancelled, "CNCL", "Cancelled", "Cancelled; stock and discount are given back"),
            (OrderStatuses.Dispatched, "DSPT", "Dispatched", "Handed to the courier"),
            (OrderStatuses.Delivered, "DLVD", "Delivered", "Delivered to the customer"));

        Configure<PaymentMethodLookup>(modelBuilder, "PAYM_METH_LKP",
            (PaymentMethods.CashOnDelivery, "COD", "Cash on Delivery", "Pay the courier when the parcel arrives"),
            (PaymentMethods.Easypaisa, "EASY", "Easypaisa", "Pay online with Easypaisa"),
            (PaymentMethods.JazzCash, "JAZZ", "JazzCash", "Pay online with JazzCash"),
            (PaymentMethods.Card, "CARD", "Card", "Pay online with a debit or credit card"));

        Configure<CancelReasonLookup>(modelBuilder, "CNCL_RSN_LKP",
            (CancelReasons.Staff, "STAF", "Staff", "Cancelled by a member of staff"),
            (CancelReasons.CodNoAnswer, "CODN", "No answer", "Cash on delivery: nobody answered the confirmation calls"),
            (CancelReasons.CodRefused, "CODR", "Refused", "Cash on delivery: the customer refused the order"),
            (CancelReasons.Customer, "CUST", "Customer", "Cancelled by the customer"),
            (CancelReasons.PaymentFailed, "PYFL", "Payment failed", "The online payment failed"),
            (CancelReasons.PaymentExpired, "PYEX", "Payment expired", "The payment window passed without payment"));

        Configure<CodOutcomeLookup>(modelBuilder, "COD_OTCM_LKP",
            (CodOutcomes.Confirmed, "CONF", "Confirmed by staff", "Staff reached the customer and the order is confirmed"),
            (CodOutcomes.NoAnswer, "NOAN", "No answer", "Nobody answered the call"),
            (CodOutcomes.Refused, "REFU", "Refused", "The customer refused the order"),
            (CodOutcomes.ConfirmedByCustomerOnWhatsApp, "CWAP", "Confirmed on WhatsApp", "The customer confirmed with the WhatsApp button"),
            (CodOutcomes.ConfirmedByCustomerOnPage, "CWEB", "Confirmed on the page", "The customer confirmed with the button on the page or in the email"),
            (CodOutcomes.CancelledByCustomerOnWhatsApp, "CCAN", "Cancelled on WhatsApp", "The customer cancelled with the WhatsApp button"),
            (CodOutcomes.CancelledByCustomerOnPage, "CWCN", "Cancelled on the page", "The customer cancelled from the page or the email"));

        Configure<ActorKindLookup>(modelBuilder, "ACTR_KIND_LKP",
            (ActorKinds.Staff, "STAF", "Staff", "A member of staff"),
            (ActorKinds.Customer, "CUST", "Customer", "The customer"),
            (ActorKinds.System, "SYST", "System", "A background job or rule"),
            (ActorKinds.Gateway, "GWAY", "Gateway", "The payment gateway"));
    }

    private static void Configure<TLookup>(ModelBuilder modelBuilder, string tableName, params (short Id, string Code, string Name, string Description)[] rows)
        where TLookup : OrdersLookup
    {
        var builder = ConfigureShape<TLookup>(modelBuilder, tableName);
        builder.HasData(rows.Select(r => (object)new
        {
            Id = r.Id, Code = r.Code, Name = r.Name, Description = (string?)r.Description, SortSeq = r.Id, IsActive = true,
        }).ToArray());
    }

    private static EntityTypeBuilder<TLookup> ConfigureShape<TLookup>(ModelBuilder modelBuilder, string tableName)
        where TLookup : OrdersLookup
    {
        var builder = modelBuilder.Entity<TLookup>();
        builder.ToTable(tableName);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(l => l.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(l => l.Code).IsUnique();
        builder.Property(l => l.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(l => l.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(l => l.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(l => l.IsActive).HasColumnName("ACT_IND").IsRequired();
        return builder;
    }
}
