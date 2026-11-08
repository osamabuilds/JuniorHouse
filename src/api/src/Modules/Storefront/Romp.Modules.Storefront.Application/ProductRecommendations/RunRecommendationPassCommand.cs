using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.ProductRecommendations;

namespace Romp.Modules.Storefront.Application.ProductRecommendations;

/// <summary>What one pass did: the groups it rescored, the products it wrote and the groups that failed.</summary>
public sealed record RecommendationPassResult(int Groups, int Products, int Failed);

/// <summary>
/// One worker pass: finds the groups to rescore (those with a product changed since the last finished run, and any group not scored within
/// <see cref="IRecommendationSettings.GroupMaxAge"/>), scores them one at a time inside the time budget, and records a <see cref="RecommendationRun"/>.
/// A pass with a failed group is left unfinished, so the watermark does not move past what that group still needs.
/// </summary>
public sealed record RunRecommendationPassCommand : IRequest<RecommendationPassResult>;

public sealed partial class RunRecommendationPassCommandHandler(
    IStorefrontReadDbContext db, ISender sender, IRecommendationSettings settings, TimeProvider clock, ILogger<RunRecommendationPassCommandHandler> logger)
    : IRequestHandler<RunRecommendationPassCommand, RecommendationPassResult>
{
    public async Task<RecommendationPassResult> Handle(RunRecommendationPassCommand request, CancellationToken cancellationToken)
    {
        var started = clock.GetTimestamp();
        var now = clock.GetUtcNow();
        var watermark = await db.RecommendationRuns.AsNoTracking()
            .Where(r => r.EndedAt != null)
            .MaxAsync(r => r.EndedAt, cancellationToken);

        var run = new RecommendationRun(now);
        db.RecommendationRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        var groups = await db.Products.AsNoTracking()
            .GroupBy(p => new { p.GenderCode, p.AgeCode })
            .Select(g => new
            {
                g.Key.GenderCode,
                g.Key.AgeCode,
                LastChange = g.Max(p => p.UpdtDte ?? p.InsrDte),
            })
            .ToListAsync(cancellationToken);
        var scored = (await db.SimilarProducts.AsNoTracking()
                .Join(db.Products, s => s.ProductId, p => p.Id, (s, p) => new { p.GenderCode, p.AgeCode, s.CalculatedAt })
                .GroupBy(x => new { x.GenderCode, x.AgeCode })
                .Select(g => new { g.Key.GenderCode, g.Key.AgeCode, Last = g.Max(x => x.CalculatedAt) })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => (x.GenderCode, x.AgeCode), x => x.Last);

        var due = groups.Where(g =>
                watermark is null
                || g.LastChange > watermark
                || !scored.TryGetValue((g.GenderCode, g.AgeCode), out var last)
                || now - last >= settings.GroupMaxAge)
            .OrderBy(g => g.GenderCode, StringComparer.Ordinal).ThenBy(g => g.AgeCode, StringComparer.Ordinal)
            .ToList();

        var done = 0;
        var written = 0;
        var failed = 0;
        foreach (var group in due)
        {
            if (done + failed > 0 && clock.GetElapsedTime(started) > settings.RunBudget)
            {
                LogBudgetReached(due.Count - done - failed);
                failed++; // groups left over: leave the run unfinished so the next pass starts from the same watermark
                break;
            }

            try
            {
                written += await sender.Send(new RefreshSimilarProductsCommand(group.GenderCode, group.AgeCode), cancellationToken);
                done++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.ClearTracker();
                failed++;
                LogGroupFailed(ex, group.GenderCode, group.AgeCode);
            }
        }

        if (failed == 0)
        {
            // Reload: a failed group clears the tracker, and a finished run must be the one saved.
            var tracked = await db.RecommendationRuns.SingleAsync(r => r.Id == run.Id, cancellationToken);
            tracked.Finish(clock.GetUtcNow(), done, written);
            await db.SaveChangesAsync(cancellationToken);
        }

        return new RecommendationPassResult(done, written, failed);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Recommendations for group {Gender}/{Age} failed; the pass goes on and will retry it.")]
    private partial void LogGroupFailed(Exception exception, string gender, string age);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recommendation pass reached its time budget with {Left} groups left for the next pass.")]
    private partial void LogBudgetReached(int left);
}
