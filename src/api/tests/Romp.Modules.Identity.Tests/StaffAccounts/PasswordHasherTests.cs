using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.StaffAccounts;

namespace Romp.Modules.Identity.Tests.StaffAccounts;

public sealed class SecretStringTests
{
    [Fact]
    [Trait("Spec", "AC-3")]
    public void ToString_NeverShowsTheValue()
    {
        var secret = new SecretString("hunter2-hunter2");

        Assert.DoesNotContain("hunter2", secret.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", $"{secret}", StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", secret), StringComparison.Ordinal);
        Assert.Equal("hunter2-hunter2", secret.Reveal());
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public void Serialising_Throws_SoTheValueCannotLeakIntoJsonOrLogs()
    {
        var secret = new SecretString("hunter2-hunter2");

        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(secret));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new { Password = secret }));
    }
}

public sealed class PasswordHasherTests
{
    private static readonly Argon2Options Fast = new() { MemoryKiB = 8, Iterations = 1, Parallelism = 1 };

    private static Argon2PasswordHasher Hasher(Argon2Options? options = null) => new(Options.Create(options ?? Fast));

    [Fact]
    [Trait("Spec", "AC-3")]
    public void Hash_IsSaltedAndVerifies()
    {
        var hasher = Hasher();
        var password = new SecretString("correct horse battery");

        var first = hasher.Hash(password);
        var second = hasher.Hash(password);

        Assert.NotEqual(first, second);
        Assert.StartsWith("$argon2id$v=19$m=8,t=1,p=1$", first, StringComparison.Ordinal);
        Assert.DoesNotContain("correct", first, StringComparison.Ordinal);
        Assert.True(hasher.Verify(password, first));
        Assert.True(hasher.Verify(password, second));
        Assert.False(hasher.Verify(new SecretString("correct horse batterz"), first));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public void Verify_UsesTheParametersStoredInTheHash_SoTheyCanBeRaisedLater()
    {
        var hash = Hasher().Hash(new SecretString("correct horse battery"));
        var stronger = Hasher(new Argon2Options { MemoryKiB = 64, Iterations = 2, Parallelism = 1 });

        Assert.True(stronger.Verify(new SecretString("correct horse battery"), hash));
        Assert.True(stronger.NeedsRehash(hash));
        Assert.False(Hasher().NeedsRehash(hash));
    }

    [Theory]
    [Trait("Spec", "AC-3")]
    [InlineData("")]
    [InlineData("plain-text")]
    [InlineData("$argon2id$v=19$m=8,t=1,p=1$notbase64!$alsonot!")]
    [InlineData("$argon2i$v=19$m=8,t=1,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Verify_ReturnsFalse_ForMalformedHashes(string stored)
    {
        Assert.False(Hasher().Verify(new SecretString("correct horse battery"), stored));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public void Hash_TakesAboutTheConfiguredTime()
    {
        var hasher = Hasher(new Argon2Options());
        var password = new SecretString("correct horse battery");
        _ = hasher.Hash(password); // warm up the JIT

        var watch = Stopwatch.StartNew();
        _ = hasher.Hash(password);
        watch.Stop();

        // Tuned for about 250 ms on the VPS; the band is wide because CI runners vary.
        Assert.InRange(watch.ElapsedMilliseconds, 40, 3000);
    }

    [Theory]
    [Trait("Spec", "AC-3")]
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
