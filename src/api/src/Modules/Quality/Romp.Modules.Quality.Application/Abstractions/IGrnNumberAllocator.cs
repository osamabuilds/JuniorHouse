namespace Romp.Modules.Quality.Application.Abstractions;

/// <summary>ADR 0006: atomically allocates the next <c>GRN-{YYYY}-{NNNNN}</c> number inside the caller's transaction.</summary>
public interface IGrnNumberAllocator
{
    Task<string> AllocateAsync(CancellationToken cancellationToken);
}
