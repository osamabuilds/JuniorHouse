using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Persistence;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Infrastructure;

namespace Romp.Modules.Vendor.Tests;

public sealed class EventPayloadTests
{
    private static async Task<(IServiceProvider Provider, ISender Sender, PoDto Po)> SentPoAsync()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));
        return (provider, sender, po);
    }

    private static List<OutboxMessage> Outbox(IServiceProvider provider) =>
        provider.GetRequiredService<VendorDbContext>().Set<OutboxMessage>().ToList();

    [Fact]
    [Trait("Spec", "AC-48")]
    public async Task Sprint1Events_V2_AdditiveOnly_NoFieldMeaningChanges()
    {
        var (provider, sender, po) = await SentPoAsync();
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));
        await sender.Send(new CancelPurchaseOrderCommand(po.Id, 6));

        var sent = JsonDocument.Parse(Outbox(provider).Single(m => m.EventType == "PoSentToVendorEvent").Payload).RootElement;
        var cancelled = JsonDocument.Parse(Outbox(provider).Single(m => m.EventType == "PoCancelledEvent").Payload).RootElement;

        // v1 fields keep their names and meaning...
        Assert.Equal(po.PoNo, sent.GetProperty("PoNo").GetString());
        Assert.Equal(po.Id, sent.GetProperty("PoId").GetInt64());
        Assert.Equal((short)6, cancelled.GetProperty("CancelReasonId").GetInt16());
        // ...and v2 only adds.
        Assert.Equal(2, sent.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal(0, sent.GetProperty("RevisionNumber").GetInt32());
        Assert.Equal(500m, sent.GetProperty("Terms").GetProperty("UnitCost").GetDecimal());
        Assert.Equal(2, sent.GetProperty("Terms").GetProperty("Lines").GetArrayLength());
    }

    [Fact]
    [Trait("Spec", "AC-49")]
    public async Task RevisionEvents_CarryFullEnvelopeAndSnapshot()
    {
        var (provider, sender, po) = await SentPoAsync();
        await sender.Send(new CreateAmendmentCommand(
            po.Id, 1, 1, "internal note", null,
            600m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines));

        var message = Outbox(provider).Single(m => m.EventType == "PoRevisionPutInForceEvent");
        var payload = JsonDocument.Parse(message.Payload).RootElement;

        Assert.NotEqual(Guid.Empty, payload.GetProperty("EventId").GetGuid());
        Assert.Equal("PoRevisionPutInForceEvent", payload.GetProperty("EventType").GetString());
        Assert.Equal(1, payload.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal("PurchaseOrder", payload.GetProperty("AggregateType").GetString());
        Assert.Equal(po.Id, payload.GetProperty("AggregateId").GetInt64());
        Assert.Equal(po.PoNo, payload.GetProperty("PoNo").GetString());
        Assert.Equal(1, payload.GetProperty("RevisionNumber").GetInt32());
        Assert.Equal(600m, payload.GetProperty("Terms").GetProperty("UnitCost").GetDecimal());
        Assert.True(payload.TryGetProperty("OccurredAt", out _));

        // The outbox row itself carries the envelope the dispatcher orders by.
        Assert.Equal("PurchaseOrder", message.AggregateType);
        Assert.Equal(po.Id, message.AggregateId);
        Assert.Equal((short)1, message.MessageVersion);
    }

    [Fact]
    [Trait("Spec", "AC-50")]
    public async Task Payload_NeverContainsInternalData()
    {
        var (provider, sender, po) = await SentPoAsync();
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));
        await sender.Send(new UploadPoFileCommand(po.Id, PoTestFiles.CostSheet, "secret-cost-sheet.pdf", PoTestFiles.Pdf()));
        await sender.Send(new CreateAmendmentCommand(
            po.Id, 1, 1, "TOP-SECRET-IMPACT-NOTE", "vendor facing message",
            600m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines));

        foreach (var message in Outbox(provider))
        {
            Assert.DoesNotContain("TOP-SECRET-IMPACT-NOTE", message.Payload);
            Assert.DoesNotContain("secret-cost-sheet", message.Payload);
            Assert.DoesNotContain("ImpactNote", message.Payload);
            Assert.DoesNotContain("Evidence", message.Payload);
            Assert.DoesNotContain("StorageKey", message.Payload);
        }
    }
}
