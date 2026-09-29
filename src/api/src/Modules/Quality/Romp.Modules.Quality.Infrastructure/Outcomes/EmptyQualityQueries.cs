using Romp.Modules.Quality.Contracts;

namespace Romp.Modules.Quality.Infrastructure.Outcomes;

/// <summary>Placeholder <see cref="IQualityQueries"/> until batch decisions exist: nothing is released, which is the safe answer (AC-24).</summary>
public sealed class EmptyQualityQueries : IQualityQueries
{
    public Task<IReadOnlyList<ReleasedLine>> GetReleasedQuantitiesAsync(long poId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ReleasedLine>>([]);

    public Task<IReadOnlyList<ReleasedLine>> GetReleasedQuantitiesByBatchAsync(long batchId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ReleasedLine>>([]);

    public Task<IReadOnlyList<QcLine>> GetReceivedQuantitiesAsync(long poId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<QcLine>>([]);
}
