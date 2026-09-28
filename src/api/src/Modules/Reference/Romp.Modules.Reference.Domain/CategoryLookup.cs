namespace Romp.Modules.Reference.Domain;

/// <summary>Maps to REF.CATG_LKP. Self-referencing via <see cref="ParentCategoryId"/> for hierarchy.</summary>
public sealed class CategoryLookup : Lookup
{
    private CategoryLookup()
    {
    }

    public CategoryLookup(string code, string name, string? description, short sortSeq, short? parentCategoryId)
        : base(code, name, description, sortSeq)
    {
        ParentCategoryId = parentCategoryId;
    }

    public short? ParentCategoryId { get; private set; }

    public void SetParent(short? parentCategoryId) => ParentCategoryId = parentCategoryId;
}
