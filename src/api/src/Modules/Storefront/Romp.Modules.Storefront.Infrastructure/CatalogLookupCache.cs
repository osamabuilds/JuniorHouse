using Romp.Modules.Storefront.Application.Abstractions;

namespace Romp.Modules.Storefront.Infrastructure;

/// <summary>One copy of the menu lookups for 60 seconds. The semaphore makes the load single-flight: callers that arrive meanwhile wait for it.</summary>
public sealed class CatalogLookupCache(TimeProvider clock) : ICatalogLookupCache, IDisposable
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private CatalogLookups? _value;
    private DateTimeOffset _expires;

    public async Task<CatalogLookups> GetOrLoadAsync(Func<CancellationToken, Task<CatalogLookups>> load, CancellationToken cancellationToken)
    {
        if (Fresh() is { } hit)
        {
            return hit;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (Fresh() is { } loadedMeanwhile)
            {
                return loadedMeanwhile;
            }

            var loaded = await load(cancellationToken);
            _value = loaded;
            _expires = clock.GetUtcNow().Add(Lifetime);
            return loaded;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private CatalogLookups? Fresh() => _value is not null && clock.GetUtcNow() < _expires ? _value : null;
}
