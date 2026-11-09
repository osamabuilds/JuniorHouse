using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using Romp.Modules.Fulfilment.Application.Abstractions;

namespace Romp.Modules.Fulfilment.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations` can build a <see cref="FulfDbContext"/> without the API host's DI container.</summary>
public sealed class FulfDbContextFactory : IDesignTimeDbContextFactory<FulfDbContext>
{
    public FulfDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=romp;Username=romp;Password=change-me-locally";

        var optionsBuilder = new DbContextOptionsBuilder<FulfDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "FULF"));

        return new FulfDbContext(optionsBuilder.Options);
    }
}

/// <summary>
/// Fulfilment's unit of work: runs the handler inside one database transaction, then saves, so the state change, its inbox row and its outbox
/// events commit together (ADR 0004, NFR-FT-08). A command sent from inside another command's handler joins the outer transaction. Applies only
/// to <see cref="IFulfilmentCommand"/> requests.
/// </summary>
public sealed class FulfilmentTransactionBehavior<TRequest, TResponse>(FulfDbContext dbContext) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IFulfilmentCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            var nested = await next(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return nested;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var response = await next(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            // A failed unit of work must not leave tracked changes behind for a later command in the same scope.
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}

/// <summary>Start-up validation of <c>Fulfilment:*</c>: a number outside its range stops the host from starting.</summary>
public sealed class FulfilmentOptionsValidator : IValidateOptions<FulfilmentOptions>
{
    public ValidateOptionsResult Validate(string? name, FulfilmentOptions options)
    {
        var failures = new List<string>();

        if (options.PolyBagMaxGrams is < 1 or > 30_000)
        {
            failures.Add("Fulfilment:PolyBagMaxGrams must be between 1 and 30000.");
        }

        if (options.PolyBagMaxItems is < 1 or > 100)
        {
            failures.Add("Fulfilment:PolyBagMaxItems must be between 1 and 100.");
        }

        if (options.WeightTolerancePercent is < 0 or > 100)
        {
            failures.Add("Fulfilment:WeightTolerancePercent must be between 0 and 100.");
        }

        if (options.WeightToleranceGrams is < 0 or > 5_000)
        {
            failures.Add("Fulfilment:WeightToleranceGrams must be between 0 and 5000.");
        }

        if (options.GarmentGrams is < 1 or > 5_000)
        {
            failures.Add("Fulfilment:GarmentGrams must be between 1 and 5000.");
        }

        if (options.PackagingGrams is < 0 or > 5_000)
        {
            failures.Add("Fulfilment:PackagingGrams must be between 0 and 5000.");
        }

        if (options.ScanLogRetentionDays is < 1 or > 3_650)
        {
            failures.Add("Fulfilment:ScanLogRetentionDays must be between 1 and 3650.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
