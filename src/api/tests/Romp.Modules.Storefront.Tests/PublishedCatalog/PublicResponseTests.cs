using System.Reflection;
using System.Text.RegularExpressions;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Tests.PublishedCatalog;

/// <summary>Published-catalog task 22: a public response never carries an exact stock quantity (PCT-3, Q-PCT-2).</summary>
public sealed partial class PublicResponseTests
{
    [GeneratedRegex("quantit|qty|on_?hand|stock(count|level|units?)|units?(left|in)|available(count|units?)|remaining", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex QuantityName();

    private static readonly Assembly ApplicationAssembly = typeof(Romp.Modules.Storefront.Application.AssemblyReference).Assembly;

    /// <summary>Every problem found in <paramref name="root"/> and the types reachable from its properties.</summary>
    private static List<string> Problems(Type root)
    {
        var problems = new List<string>();
        Walk(root, root.Name, new HashSet<Type>(), problems);
        return problems;
    }

    private static void Walk(Type type, string path, HashSet<Type> seen, List<string> problems)
    {
        type = Underlying(type);
        if (!seen.Add(type) || type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
        {
            return;
        }

        if (IsEntity(type))
        {
            problems.Add($"{path}: exposes the read-model entity {type.Name}");
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var here = $"{path}.{property.Name}";
            if (QuantityName().IsMatch(property.Name))
            {
                problems.Add($"{here}: looks like a stock quantity");
            }

            var propertyType = Underlying(property.PropertyType);
            if (propertyType == typeof(AvailabilityClass))
            {
                continue;
            }

            Walk(property.PropertyType, here, seen, problems);
        }
    }

    private static Type Underlying(Type type)
    {
        if (type.IsArray)
        {
            return Underlying(type.GetElementType()!);
        }

        if (Nullable.GetUnderlyingType(type) is { } inner)
        {
            return Underlying(inner);
        }

        return type.IsGenericType && type.GetGenericArguments().Length == 1 ? Underlying(type.GetGenericArguments()[0]) : type;
    }

    private static bool IsEntity(Type type)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Entity<>))
            {
                return true;
            }
        }

        return false;
    }

    public static TheoryData<string> PublicResponseNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var type in ApplicationAssembly.GetTypes().Where(t => typeof(IPublicStorefrontResponse).IsAssignableFrom(t) && t is { IsInterface: false }))
            {
                data.Add(type.FullName!);
            }

            // Keeps the theory from being empty (xUnit refuses that) until the first public DTO exists; the marker itself always passes.
            data.Add(typeof(IPublicStorefrontResponse).FullName!);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(PublicResponseNames))]
    [Trait("Spec", "PCT-3")]
    public void APublicResponse_CarriesNoExactStockQuantity(string typeName)
    {
        var type = ApplicationAssembly.GetType(typeName, throwOnError: true)!;

        Assert.Empty(Problems(type));
    }

    [Fact]
    [Trait("Spec", "PCT-3")]
    public void TheGuard_CatchesAQuantity_OnTheTypeAndNested()
    {
        Assert.NotEmpty(Problems(typeof(LeaksQuantity)));
        Assert.NotEmpty(Problems(typeof(LeaksNested)));
        Assert.NotEmpty(Problems(typeof(LeaksEntity)));
    }

    [Fact]
    [Trait("Spec", "PCT-3")]
    public void TheGuard_AllowsAnAvailabilityClass() => Assert.Empty(Problems(typeof(SafeVariant)));

    [Fact]
    [Trait("Spec", "PCT-3")]
    public void EveryPublicResponse_ImplementsNoAdminOrCommandMarker()
    {
        var offenders = ApplicationAssembly.GetTypes()
            .Where(t => typeof(IPublicStorefrontResponse).IsAssignableFrom(t) && typeof(IStorefrontCommand).IsAssignableFrom(t))
            .Select(t => t.FullName);

        Assert.Empty(offenders);
    }

    private sealed record LeaksQuantity(string Sku, int AvailableQuantity);

    private sealed record LeaksNested(IReadOnlyList<LeaksQuantity> Variants);

    private sealed record LeaksEntity(VariantRow Variant);

    private sealed record SafeVariant(string Sku, AvailabilityClass Availability);
}
