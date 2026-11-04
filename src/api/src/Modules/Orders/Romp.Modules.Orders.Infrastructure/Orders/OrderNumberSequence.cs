using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Infrastructure.Persistence;

namespace Romp.Modules.Orders.Infrastructure.Orders;

/// <summary>ORDR.ORDR_NO_SEQ: the order counter per calendar year (ADR 0006). Written only by <see cref="OrderNumberAllocator"/>.</summary>
internal sealed class OrderNumberSequence
{
    public short Year { get; set; }

    public int Sequence { get; set; }
}

internal sealed class OrderNumberSequenceConfiguration : IEntityTypeConfiguration<OrderNumberSequence>
{
    public void Configure(EntityTypeBuilder<OrderNumberSequence> builder)
    {
        builder.ToTable("ORDR_NO_SEQ");
        builder.HasKey(s => s.Year);
        builder.Property(s => s.Year).HasColumnName("YR").ValueGeneratedNever();
        builder.Property(s => s.Sequence).HasColumnName("SEQ").IsRequired();
    }
}

/// <summary>
/// One atomic statement per number: the upsert row-locks the year, so two concurrent orders never read the same value (ADR 0006). A number
/// taken by a transaction that later rolls back is not reused, which the format allows (it promises order, not gaplessness).
/// </summary>
public sealed class OrderNumberAllocator(OrdrDbContext dbContext) : IOrderNumberAllocator
{
    private const string Sql =
        """
        INSERT INTO "ORDR"."ORDR_NO_SEQ" ("YR", "SEQ") VALUES ({0}, 1)
        ON CONFLICT ("YR") DO UPDATE SET "SEQ" = "ORDR"."ORDR_NO_SEQ"."SEQ" + 1
        RETURNING "SEQ" AS "Value";
        """;

    public async Task<string> NextAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var year = (short)now.UtcDateTime.Year;
        var rows = await dbContext.Database.SqlQueryRaw<int>(Sql, year).ToListAsync(cancellationToken);
        return OrderNumbers.Format(year, rows.Single());
    }
}
