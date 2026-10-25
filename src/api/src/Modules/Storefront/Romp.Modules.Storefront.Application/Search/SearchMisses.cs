using MediatR;
using Romp.Modules.Storefront.Domain.Search;

namespace Romp.Modules.Storefront.Application.Search;

/// <summary>Where zero-result searches are counted. Implemented in Infrastructure with a single upsert (search plan, Concurrency).</summary>
public interface ISearchMissStore
{
    /// <summary>Adds one to the count of <paramref name="text"/> on <paramref name="day"/>, creating the row the first time.</summary>
    Task RecordAsync(string text, DateOnly day, CancellationToken cancellationToken);

    /// <summary>Deletes counts of days before <paramref name="before"/>; returns how many rows went.</summary>
    Task<int> PurgeAsync(DateOnly before, CancellationToken cancellationToken);
}

/// <summary>
/// Takes the text of a search that found nothing, to be recorded after the response (SRC-13). Never throws and never waits: a full queue
/// drops the text, because a lost count must not slow or fail a visitor's search.
/// </summary>
public interface ISearchMissSink
{
    void Offer(string? text);
}

/// <summary>
/// Counts a zero-result search for the business (SRC-13): normalised, cut at 100 characters, dropped when it looks like an email address,
/// a phone number or a link. No IP, no account, no session id is part of the command, so none can be stored.
/// </summary>
public sealed record RecordMissCommand(string? Text) : IRequest<bool>;

public sealed class RecordMissCommandHandler(ISearchMissStore store, TimeProvider clock) : IRequestHandler<RecordMissCommand, bool>
{
    public async Task<bool> Handle(RecordMissCommand request, CancellationToken cancellationToken)
    {
        if (MissRedactor.ForStorage(SearchQuery.Parse(request.Text)) is not { } text)
        {
            return false;
        }

        await store.RecordAsync(text, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), cancellationToken);
        return true;
    }
}

/// <summary>Removes counts older than <see cref="ISearchSettings.MissRetentionDays"/> (SRC-13 retention).</summary>
public sealed record PurgeMissesCommand : IRequest<int>;

public sealed class PurgeMissesCommandHandler(ISearchMissStore store, ISearchSettings settings, TimeProvider clock) : IRequestHandler<PurgeMissesCommand, int>
{
    public Task<int> Handle(PurgeMissesCommand request, CancellationToken cancellationToken) =>
        store.PurgeAsync(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(-settings.MissRetentionDays), cancellationToken);
}
