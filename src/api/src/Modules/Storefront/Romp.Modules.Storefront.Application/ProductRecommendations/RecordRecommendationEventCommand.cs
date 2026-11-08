using MediatR;
using Romp.Modules.Storefront.Domain.ProductRecommendations;

namespace Romp.Modules.Storefront.Application.ProductRecommendations;

/// <summary>Adds to the day counters of a row and product atomically. Implemented in Infrastructure as one SQL statement (REC-12).</summary>
public interface IRecommendationEventStore
{
    /// <summary>Adds the counts for the day; false (nothing written) when the product is not in the read model.</summary>
    Task<bool> AddAsync(RecommendationRowId row, long productId, DateOnly day, int clicks, int adds, CancellationToken cancellationToken);
}

public enum RecommendationEventOutcome
{
    Recorded,
    Invalid,
    UnknownProduct,
}

/// <summary>
/// A recommendation card was clicked or had a size added from it (REC-12). The row is <c>SIMILAR</c> or <c>TRENDING</c>, the action <c>click</c> or
/// <c>add</c>. Only a counter changes: no cookie, address or account is read or kept.
/// </summary>
public sealed record RecordRecommendationEventCommand(string? RowCode, long ProductId, string? Action) : IRequest<RecommendationEventOutcome>;

public sealed class RecordRecommendationEventCommandHandler(IRecommendationEventStore store, TimeProvider clock)
    : IRequestHandler<RecordRecommendationEventCommand, RecommendationEventOutcome>
{
    public async Task<RecommendationEventOutcome> Handle(RecordRecommendationEventCommand request, CancellationToken cancellationToken)
    {
        var row = request.RowCode switch
        {
            "SIMILAR" => (RecommendationRowId?)RecommendationRowId.Similar,
            "TRENDING" => RecommendationRowId.Trending,
            _ => null,
        };
        var isClick = request.Action == "click";
        if (row is null || request.ProductId <= 0 || (!isClick && request.Action != "add"))
        {
            return RecommendationEventOutcome.Invalid;
        }

        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var recorded = await store.AddAsync(row.Value, request.ProductId, day, isClick ? 1 : 0, isClick ? 0 : 1, cancellationToken);
        return recorded ? RecommendationEventOutcome.Recorded : RecommendationEventOutcome.UnknownProduct;
    }
}
