using Romp.Api.HostVerbs;
using Romp.BuildingBlocks.Persistence.Auditing;

namespace Romp.Api.IntegrationTests.HostVerbs;

/// <summary>G8: operator verbs are parsed before the web host starts; secrets never come from the command line.</summary>
public sealed class HostVerbTests
{
    // ---- parser -------------------------------------------------------------------------

    [Theory]
    [InlineData]
    [InlineData("--urls=http://localhost:5051")]
    [InlineData("--environment", "Production")]
    public void Parse_NoVerb_MeansRunTheWebHost(params string[] args)
    {
        var result = HostVerbParser.Parse(args);

        Assert.Null(result.Request);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("create-owner")]
    [InlineData("reset-owner")]
    [InlineData("migrate")]
    [InlineData("healthcheck")]
    [InlineData("rebuild-storefront")]
    public void Parse_KnownVerb_IsReturned(string verb)
    {
        var result = HostVerbParser.Parse([verb]);

        Assert.Null(result.Error);
        Assert.Equal(verb, result.Request!.Verb);
        Assert.Empty(result.Request.Flags);
    }

    [Fact]
    public void Parse_ResetOwner_AcceptsItsFlags()
    {
        var result = HostVerbParser.Parse(["reset-owner", "--password", "--mfa"]);

        Assert.Null(result.Error);
        Assert.True(result.Request!.Has("--password"));
        Assert.True(result.Request.Has("--mfa"));
    }

    [Theory]
    [InlineData("frobnicate")]
    [InlineData("Migrate")]
    [InlineData("migrate;rm")]
    public void Parse_UnknownVerb_Fails_AndListsTheKnownOnes(string verb)
    {
        var result = HostVerbParser.Parse([verb]);

        Assert.Null(result.Request);
        Assert.Contains("create-owner", result.Error);
        Assert.Contains("migrate", result.Error);
    }

    [Theory]
    [InlineData("create-owner", "--password=hunter2hunter2")]
    [InlineData("create-owner", "--password", "hunter2hunter2")]
    [InlineData("reset-owner", "--password", "hunter2hunter2")]
    [InlineData("reset-owner", "--password=hunter2hunter2", "--mfa")]
    [InlineData("reset-owner", "--mfa-code=123456")]
    [InlineData("create-owner", "--secret", "hunter2hunter2")]
    [InlineData("create-owner", "hunter2hunter2")]
    public void Parse_ASecretOnTheCommandLine_IsRejected_WithoutEchoingIt(params string[] args)
    {
        var result = HostVerbParser.Parse(args);

        Assert.Null(result.Request);
        Assert.NotNull(result.Error);
        Assert.DoesNotContain("hunter2", result.Error);
        Assert.DoesNotContain("123456", result.Error);
    }

    [Fact]
    public void Parse_AFlagTheVerbDoesNotKnow_IsRejected()
    {
        var result = HostVerbParser.Parse(["migrate", "--mfa"]);

        Assert.Null(result.Request);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Parse_CreateOwner_TakesNoPasswordFlag_BecauseTheSecretComesFromEnvironmentOrPrompt()
    {
        Assert.NotNull(HostVerbParser.Parse(["create-owner", "--password"]).Error);
    }

    // ---- secret input -------------------------------------------------------------------

    [Fact]
    public void ReadSecret_PrefersTheEnvironmentVariable()
    {
        var reader = new OperatorSecretReader(name => name == "ROMP_OWNER_PASSWORD" ? "from-env-value" : null, _ => "from-prompt");

        Assert.Equal("from-env-value", reader.Read("ROMP_OWNER_PASSWORD", "Password: "));
    }

    [Fact]
    public void ReadSecret_FallsBackToTheNoEchoPrompt()
    {
        var reader = new OperatorSecretReader(_ => null, prompt => prompt == "Password: " ? "typed-value" : null);

        Assert.Equal("typed-value", reader.Read("ROMP_OWNER_PASSWORD", "Password: "));
    }

    [Fact]
    public void ReadSecret_WithNeitherSource_Fails_NamingTheVariableButNoValue()
    {
        var reader = new OperatorSecretReader(_ => "  ", _ => null);

        var ex = Assert.Throws<InvalidOperationException>(() => reader.Read("ROMP_OWNER_PASSWORD", "Password: "));
        Assert.Contains("ROMP_OWNER_PASSWORD", ex.Message);
    }

    // ---- runner -------------------------------------------------------------------------

    private static (int Code, string Out, string Err) Run(HostVerbRequest request, Dictionary<string, HostVerbHandler> handlers)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = HostVerbRunner.RunAsync(request, new Dictionary<string, HostVerbHandler>(handlers), stdout, stderr, CancellationToken.None)
            .GetAwaiter().GetResult();
        return (code, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void Run_CallsTheHandler_AsOperatorCli_AndReturnsItsExitCode()
    {
        string? actor = null;
        var handlers = new Dictionary<string, HostVerbHandler>
        {
            ["migrate"] = (_, _, _) => { actor = ActorScope.Current; return Task.FromResult(0); },
        };

        var (code, _, _) = Run(new HostVerbRequest("migrate", new HashSet<string>()), handlers);

        Assert.Equal(0, code);
        Assert.Equal("operator-cli", actor);
        Assert.Null(ActorScope.Current);
    }

    [Fact]
    public void Run_AVerbWithoutAHandlerYet_ExitsNonZero_AndSaysSo()
    {
        var (code, _, err) = Run(new HostVerbRequest("create-owner", new HashSet<string>()), []);

        Assert.NotEqual(0, code);
        Assert.Contains("create-owner", err);
    }

    [Fact]
    public void Run_AHandlerThatThrows_ExitsNonZero_WithoutPrintingTheMessageOrStack()
    {
        var handlers = new Dictionary<string, HostVerbHandler>
        {
            ["migrate"] = (_, _, _) => throw new InvalidOperationException("password=hunter2hunter2"),
        };

        var (code, o, err) = Run(new HostVerbRequest("migrate", new HashSet<string>()), handlers);

        Assert.NotEqual(0, code);
        Assert.DoesNotContain("hunter2", o + err);
        Assert.Contains(nameof(InvalidOperationException), err);
    }
}
