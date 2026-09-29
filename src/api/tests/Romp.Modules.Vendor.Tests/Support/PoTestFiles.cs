using Romp.BuildingBlocks.Storage;
using Romp.Modules.Vendor.Application.Files;

namespace Romp.Modules.Vendor.Tests.Support;

/// <summary>Keeps stored bytes in memory; the count lets a test prove nothing was stored for a rejected upload.</summary>
internal sealed class InMemoryFileStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _files = [];

    public int Count => _files.Count;

    public Task<string> SaveAsync(byte[] content, CancellationToken cancellationToken)
    {
        var key = Guid.NewGuid().ToString("N");
        _files[key] = content;
        return Task.FromResult(key);
    }

    public Task<byte[]> RetrieveAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult(_files[storageKey]);
}

internal static class PoTestFiles
{
    public const short TechPack = 1;
    public const short CostSheet = 6;

    public static byte[] Pdf(string body = "x") => System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 " + body);
}

/// <summary>Stands in for PROD's read-only contract in VNDR's unit tests (ADR 0008): a test sets what has been delivered / approved.</summary>
internal sealed class FakeProductionQueries : Romp.Modules.Production.Contracts.IProductionQueries
{
    public List<Romp.Modules.Production.Contracts.DeliveredQuantity> Delivered { get; } = [];

    public bool PpSampleApproved { get; set; }

    public Task<IReadOnlyList<Romp.Modules.Production.Contracts.DeliveredQuantity>> GetDeliveredQuantitiesAsync(long poId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Romp.Modules.Production.Contracts.DeliveredQuantity>>(Delivered.ToList());

    public Task<bool> HasApprovedPpSampleAsync(long poId, CancellationToken cancellationToken) => Task.FromResult(PpSampleApproved);
}
