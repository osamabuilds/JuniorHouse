using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Storage;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Application.PublishedCatalog;

public enum DerivativeOutcome
{
    /// <summary>No pending derivative is due (or every due one is claimed by another worker).</summary>
    NothingDue = 0,

    /// <summary>The file was written and the product's image-ready flag recomputed.</summary>
    Done = 1,

    /// <summary>The attempt failed and the row is due again after a backoff.</summary>
    WillRetry = 2,

    /// <summary>The row is failed for good: the original is unusable or the attempts ran out.</summary>
    Failed = 3,
}

/// <summary>
/// Makes one derivative (PCT-7, 10): claims the oldest due pending row with <c>FOR UPDATE SKIP LOCKED</c> so concurrent workers never
/// take the same row, reads the original, resizes it, writes it under its hashed name and marks the row done; a failure is recorded with a
/// backoff. Runs in one transaction, so the row lock is held until the outcome is saved. The product's image-ready flag is recomputed
/// whenever a row ends done or failed.
/// </summary>
public sealed record ProcessDerivativeCommand : IRequest<DerivativeOutcome>, IStorefrontCommand;

public sealed class ProcessDerivativeCommandHandler(
    IStorefrontReadDbContext db,
    IFileStorage storage,
    IImageResizer resizer,
    IReadModelSettings settings,
    TimeProvider clock) : IRequestHandler<ProcessDerivativeCommand, DerivativeOutcome>
{
    public async Task<DerivativeOutcome> Handle(ProcessDerivativeCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var derivative = await db.Derivatives
            .FromSqlRaw(
                """
                SELECT d.*, d.xmin FROM "STFR"."IMG_DRV" d
                WHERE d."DRV_STS_ID" = 1 AND (d."NXT_ATMP_DTE" IS NULL OR d."NXT_ATMP_DTE" <= {0})
                ORDER BY d."ID" LIMIT 1 FOR UPDATE SKIP LOCKED
                """,
                now)
            .AsTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (derivative is null)
        {
            return DerivativeOutcome.NothingDue;
        }

        var image = await db.Images.SingleAsync(i => i.Id == derivative.ImageId, cancellationToken);
        var outcome = await TryMakeAsync(derivative, image, now, cancellationToken);
        if (outcome != DerivativeOutcome.WillRetry)
        {
            var product = await db.Products.SingleAsync(p => p.Id == image.ProductId, cancellationToken);
            await ReadModelProjector.RecomputeAsync(db, product, settings, cancellationToken);
        }

        return outcome;
    }

    private async Task<DerivativeOutcome> TryMakeAsync(DerivativeRow derivative, ImageRow image, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            var original = await storage.RetrieveAsync(image.FileKey, cancellationToken);
            var hash = DerivativeName.HashOf(original);
            if (!string.Equals(image.ContentHash, hash, StringComparison.Ordinal))
            {
                image.SetContentHash(hash);
            }

            // Header first: an image bomb is refused here, before any pixels are decoded.
            resizer.Probe(original, settings.MaxImagePixels);
            var bytes = resizer.Resize(original, derivative.Width, derivative.Format, settings.ImageQuality, settings.MaxImagePixels);
            var name = DerivativeName.For(hash, derivative.Width, derivative.Format);
            await storage.SaveNamedAsync(name, bytes, cancellationToken);
            derivative.MarkDone(name);
            return DerivativeOutcome.Done;
        }
        catch (ImageRejectedException rejected)
        {
            derivative.RecordFailure(rejected.Code, permanent: true, now, settings.MaxDerivativeAttempts, settings.DerivativeRetryDelay);
            return DerivativeOutcome.Failed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var code = ex is FileNotFoundException or DirectoryNotFoundException ? "ORIGINAL_MISSING" : "STORAGE_ERROR";
            derivative.RecordFailure(code, permanent: false, now, settings.MaxDerivativeAttempts, settings.DerivativeRetryDelay);
            return derivative.Status == DerivativeStatus.Failed ? DerivativeOutcome.Failed : DerivativeOutcome.WillRetry;
        }
    }
}
