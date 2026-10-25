using Romp.Modules.Identity.Infrastructure.StaffAccounts;

namespace Romp.Modules.Identity.Tests.StaffAccounts;

public sealed class TemporaryPasswordGeneratorTests
{
    private const string LookAlikes = "0O1lI|5S2Z8B";

    [Fact]
    [Trait("Spec", "AC-4")]
    public void Generates_LongUnambiguousDistinctValues()
    {
        var generator = new TemporaryPasswordGenerator();

        var values = Enumerable.Range(0, 500).Select(_ => generator.Generate().Reveal()).ToArray();

        Assert.All(values, v =>
        {
            Assert.True(v.Length >= 16, $"too short: {v.Length}");
            Assert.All(v, c => Assert.DoesNotContain(c, LookAlikes));
            Assert.All(v, c => Assert.True(char.IsAsciiLetterOrDigit(c)));
            Assert.Contains(v, char.IsAsciiLetterLower);
            Assert.Contains(v, char.IsAsciiLetterUpper);
            Assert.Contains(v, char.IsAsciiDigit);
        });
        Assert.Equal(values.Length, values.Distinct(StringComparer.Ordinal).Count());
    }
}
