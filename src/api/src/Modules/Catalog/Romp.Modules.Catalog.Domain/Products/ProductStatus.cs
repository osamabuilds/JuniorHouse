using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Catalog.Domain.Products;

/// <summary>The lifecycle of a product (PRD-3, PRD-6). The numbers are the ids of the <c>PRDT_STS_LKP</c> rows.</summary>
public enum ProductStatus : short
{
    Draft = 1,
    Published = 2,
    Unpublished = 3,
    Archived = 4,
}

/// <summary>Maps to CTLG.PRDT_STS_LKP (standard lookup shape); seeded by migration, the ids are fixed by <see cref="ProductStatus"/>.</summary>
public sealed class ProductStatusLookup : Entity<short>
{
    private ProductStatusLookup()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public short SortSeq { get; private set; }

    public bool IsActive { get; private set; } = true;
}
