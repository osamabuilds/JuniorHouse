using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Infrastructure.Accounts;

namespace Romp.Modules.Customers.Tests.Accounts;

public sealed class PasswordHasherTests
{
    private static readonly Argon2Options Fast = new() { MemoryKiB = 8, Iterations = 1, Parallelism = 1 };

    private static Argon2PasswordHasher Hasher(Argon2Options? options = null) => new(Options.Create(options ?? Fast));

    [Fact]
    [Trait("Spec", "ACT-4")]
    public void Hash_IsSaltedAndVerifies()
    {
        var hasher = Hasher();
        var password = new SecretString("Correct1Horse");

        var first = hasher.Hash(password);
        var second = hasher.Hash(password);

        Assert.NotEqual(first, second);
        Assert.StartsWith("$argon2id$v=19$m=8,t=1,p=1$", first, StringComparison.Ordinal);
        Assert.DoesNotContain("Correct1Horse", first, StringComparison.Ordinal);
        Assert.True(hasher.Verify(password, first));
        Assert.True(hasher.Verify(password, second));
        Assert.False(hasher.Verify(new SecretString("Correct1Horsf"), first));
    }

    [Fact]
    [Trait("Spec", "ACT-4")]
    public void Verify_EmptyPassword_IsFalse_AndDoesNotThrow()
    {
        var hasher = Hasher();
        var stored = hasher.Hash(new SecretString("Correct1Horse"));

        Assert.False(hasher.Verify(new SecretString(string.Empty), stored));
    }

    [Fact]
    [Trait("Spec", "ACT-4")]
    public void Verify_UsesTheParametersStoredInTheHash_SoTheyCanBeRaisedLater()
    {
        var hash = Hasher().Hash(new SecretString("Correct1Horse"));
        var stronger = Hasher(new Argon2Options { MemoryKiB = 64, Iterations = 2, Parallelism = 1 });

        Assert.True(stronger.Verify(new SecretString("Correct1Horse"), hash));
        Assert.True(stronger.NeedsRehash(hash));
        Assert.False(Hasher().NeedsRehash(hash));
    }

    [Theory]
    [Trait("Spec", "ACT-4")]
    [InlineData("")]
    [InlineData("plain-text")]
    [InlineData("$argon2id$v=19$m=8,t=1,p=1$notbase64!$alsonot!")]
    [InlineData("$argon2i$v=19$m=8,t=1,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Verify_ReturnsFalse_ForMalformedHashes(string stored)
    {
        Assert.False(Hasher().Verify(new SecretString("Correct1Horse"), stored));
    }

    [Theory]
    [Trait("Spec", "ACT-4")]
    [InlineData(4096, 3, 1, "Production", false)]
    [InlineData(65536, 1, 1, "Production", false)]
    [InlineData(65536, 3, 0, "Production", false)]
    [InlineData(65536, 3, 1, "Production", true)]
    [InlineData(8, 1, 1, "Development", true)]
    [InlineData(0, 1, 1, "Development", false)]
    public void Validator_RejectsWeakValuesOutsideDevelopment(int memory, int iterations, int parallelism, string environment, bool valid)
    {
        var validator = new Argon2OptionsValidator(new FakeEnvironment(environment));

        var result = validator.Validate(null, new Argon2Options { MemoryKiB = memory, Iterations = iterations, Parallelism = parallelism });

        Assert.Equal(valid, result.Succeeded);
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "test";

        public string ContentRootPath { get; set; } = "/";

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
