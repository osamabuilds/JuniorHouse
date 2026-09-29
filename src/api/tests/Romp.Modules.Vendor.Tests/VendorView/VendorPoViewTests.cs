using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application.Files;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Application.Revisions;
using Romp.Modules.Vendor.Application.VendorView;
using Romp.Modules.Vendor.Tests.Support;
using System.Reflection;

namespace Romp.Modules.Vendor.Tests.VendorView;

public sealed class VendorPoViewDtoTests
{
    /// <summary>Every property name reachable from the vendor view, however deeply nested.</summary>
    private static IEnumerable<string> AllPropertyNames(Type type, HashSet<Type>? seen = null)
    {
        seen ??= [];
        if (!seen.Add(type))
        {
            yield break;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            yield return property.Name;

            var propertyType = property.PropertyType;
            var elementType = propertyType.IsGenericType ? propertyType.GetGenericArguments()[0] : propertyType;
            elementType = Nullable.GetUnderlyingType(elementType) ?? elementType;

            if (elementType.Namespace == typeof(VendorPoViewDto).Namespace)
            {
                foreach (var nested in AllPropertyNames(elementType, seen))
                {
                    yield return nested;
                }
            }
        }
    }

    [Fact]
    [Trait("Spec", "AC-36")]
    public void Type_DoesNotExposeInternalFields()
    {
        var names = AllPropertyNames(typeof(VendorPoViewDto)).ToList();

        string[] forbiddenFragments =
        [
            "ImpactNote", "PoValue", "AdvanceAmount", "QuantityDiff", "ShiftDays", "BeyondLatest",
            "TargetCost", "Retail", "Evidence", "InsrBy", "UpdtBy", "StorageKey", "StatusHistory", "Communication",
        ];

        foreach (var fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(names, name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }
}

public sealed class GetVendorFacingPoViewQueryHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-35")]
    public async Task Handle_ReturnsExpectedShapeIncludingPendingMarker()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new UploadPoFileCommand(po.Id, PoTestFiles.TechPack, "spec.pdf", PoTestFiles.Pdf()));
        await sender.Send(new UploadPoFileCommand(po.Id, PoTestFiles.CostSheet, "cost.pdf", PoTestFiles.Pdf()));
        await sender.Send(new SendPurchaseOrderCommand(po.Id));
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));
        await sender.Send(new CreateAmendmentCommand(
            po.Id, 1, 1, "internal only note", "Please confirm new price",
            600m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines));

        var view = await sender.Send(new GetVendorFacingPoViewQuery(po.Id));

        Assert.NotNull(view);
        Assert.Equal(po.PoNo, view.PoNo);
        Assert.Equal("Test Vendor", view.VendorName);
        Assert.Equal((short)0, view.RevisionNumber);
        Assert.Equal(500m, view.UnitCost); // the in-force terms, not the pending proposal
        Assert.Equal("spec.pdf", Assert.Single(view.Files).FileName); // internal cost sheet never listed
        Assert.NotNull(view.PendingRevision);
        Assert.Equal(600m, view.PendingRevision.UnitCost);
        Assert.Equal("Please confirm new price", view.PendingRevision.VendorMessage);
    }

    [Fact]
    [Trait("Spec", "AC-35")]
    public async Task Handle_DraftPo_ReturnsNull()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);

        Assert.Null(await sender.Send(new GetVendorFacingPoViewQuery(po.Id)));
    }
}
