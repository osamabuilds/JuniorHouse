using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Romp.Modules.Catalog.Infrastructure;
using Romp.Modules.Reference.Infrastructure;
using Romp.Modules.Vendor.Infrastructure;

namespace Romp.ArchitectureTests;

/// <summary>
/// Enforces docs/db/naming.md against every DbContext's EF model: every table/column/key/foreign-
/// key/index name must be UPPERCASE snake_case built from 2-4 character abbreviated words, and
/// every foreign key column must be covered by an index. Written while every context is still
/// empty or near-empty (SCRUM-170) so the first real table added from here on is checked from its
/// first commit - nothing added later is exempt.
/// </summary>
public sealed partial class NamingConventionTests
{
    [GeneratedRegex("^[A-Z0-9]{2,4}(_[A-Z0-9]{2,4})*$")]
    private static partial Regex IdentifierPattern();

    // A syntactically valid connection string that is never opened: building an EF model doesn't
    // connect to the database, only `Database.Migrate()`/queries do.
    private const string UnusedConnectionString = "Host=localhost;Database=romp;Username=romp;Password=x";

    public static TheoryData<string, IModel> Contexts()
    {
        var data = new TheoryData<string, IModel>
        {
            { "REF", BuildModel<ReferenceDbContext>(options => new ReferenceDbContext(options)) },
            { "CTLG", BuildModel<CatalogDbContext>(options => new CatalogDbContext(options)) },
            { "VNDR", BuildModel<VendorDbContext>(options => new VendorDbContext(options)) },
        };

        return data;
    }

    private static IModel BuildModel<TContext>(Func<DbContextOptions<TContext>, TContext> factory)
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(UnusedConnectionString)
            .Options;

        using var context = factory(options);
        return context.Model;
    }

    [Theory]
    [MemberData(nameof(Contexts))]
    public void AllTableAndColumnNames_MatchAbbreviationPattern(string schema, IModel model)
    {
        var violations = new List<string>();

        foreach (var entityType in model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();

            if (tableName is not null && !IdentifierPattern().IsMatch(tableName))
            {
                violations.Add($"Table \"{schema}\".\"{tableName}\" does not match the naming pattern.");
            }

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName();

                // PostgreSQL's own built-in system column (AuditableEntityTypeBuilderExtensions'
                // concurrency token) - not one of ours, can't be renamed to fit the convention.
                if (string.Equals(columnName, "xmin", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (columnName is not null && !IdentifierPattern().IsMatch(columnName))
                {
                    violations.Add(
                        $"Column \"{schema}\".\"{tableName}\".\"{columnName}\" does not match the naming pattern.");
                }
            }

            foreach (var key in entityType.GetKeys())
            {
                var keyName = key.GetName();

                if (keyName is not null && !IdentifierPattern().IsMatch(keyName))
                {
                    violations.Add($"Key \"{keyName}\" on \"{schema}\".\"{tableName}\" does not match the naming pattern.");
                }
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                var constraintName = foreignKey.GetConstraintName();

                if (constraintName is not null && !IdentifierPattern().IsMatch(constraintName))
                {
                    violations.Add(
                        $"Foreign key \"{constraintName}\" on \"{schema}\".\"{tableName}\" does not match the naming pattern.");
                }
            }

            foreach (var index in entityType.GetIndexes())
            {
                var indexName = index.GetDatabaseName();

                if (indexName is not null && !IdentifierPattern().IsMatch(indexName))
                {
                    violations.Add(
                        $"Index \"{indexName}\" on \"{schema}\".\"{tableName}\" does not match the naming pattern.");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [MemberData(nameof(Contexts))]
    public void AllForeignKeyColumns_AreIndexed(string schema, IModel model)
    {
        var violations = new List<string>();

        foreach (var entityType in model.GetEntityTypes())
        {
            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                var foreignKeyColumns = foreignKey.Properties.Select(p => p.Name).ToList();

                var indexPropertyLists = entityType.GetIndexes()
                    .Select(index => index.Properties.Select(p => p.Name).ToList());
                var keyPropertyLists = entityType.GetKeys()
                    .Select(key => key.Properties.Select(p => p.Name).ToList());

                var isCovered = indexPropertyLists.Concat(keyPropertyLists)
                    .Any(columns => StartsWith(columns, foreignKeyColumns));

                if (!isCovered)
                {
                    violations.Add(
                        $"Foreign key on \"{schema}\".\"{entityType.GetTableName()}\".({string.Join(",", foreignKeyColumns)}) has no covering index.");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    /// <summary>An index only speeds up a lookup on a foreign key if the FK's columns are a leading prefix of it.</summary>
    private static bool StartsWith(List<string> indexColumns, List<string> prefix)
    {
        if (indexColumns.Count < prefix.Count)
        {
            return false;
        }

        return !prefix.Where((t, i) => indexColumns[i] != t).Any();
    }
}
