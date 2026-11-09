namespace Romp.Api.HostVerbs;

/// <summary>A parsed operator verb: its name and the boolean flags given. Verbs never carry values (secrets come from the environment or a prompt).</summary>
public sealed record HostVerbRequest(string Verb, IReadOnlySet<string> Flags)
{
    public bool Has(string flag) => Flags.Contains(flag);
}

/// <summary>
/// Either no verb (run the web host), a request, or an error message that is safe to print: it never repeats
/// anything the operator typed, so a secret pasted onto the command line cannot reach a terminal log.
/// </summary>
public sealed record HostVerbParseResult(HostVerbRequest? Request, string? Error);

/// <summary>
/// Operator verbs on the host executable (identity plan): <c>dotnet Romp.Api.dll &lt;verb&gt;</c>. A verb is the first argument and is
/// handled before the web host starts. Flags are exact, valueless switches; <c>--key=value</c> and stray tokens are refused.
/// </summary>
public static class HostVerbParser
{
    private static readonly IReadOnlySet<string> NoFlags = new HashSet<string>();

    private static readonly Dictionary<string, IReadOnlySet<string>> Verbs = new(StringComparer.Ordinal)
    {
        ["create-owner"] = NoFlags,
        ["reset-owner"] = new HashSet<string> { "--password", "--mfa" },
        ["migrate"] = NoFlags,
        ["healthcheck"] = NoFlags,
        ["rebuild-storefront"] = NoFlags,
    };

    public static HostVerbParseResult Parse(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith('-'))
        {
            return new HostVerbParseResult(null, null);
        }

        if (!Verbs.TryGetValue(args[0], out var allowed))
        {
            return new HostVerbParseResult(null, $"Unknown operator verb. Known verbs: {string.Join(", ", Verbs.Keys.Order(StringComparer.Ordinal))}.");
        }

        var flags = new HashSet<string>(StringComparer.Ordinal);
        foreach (var arg in args.Skip(1))
        {
            if (!allowed.Contains(arg))
            {
                return new HostVerbParseResult(null,
                    "Unexpected argument for this verb. Operator verbs take only their own switches; " +
                    "secrets must never be passed on the command line (use the environment variable or the prompt).");
            }

            flags.Add(arg);
        }

        return new HostVerbParseResult(new HostVerbRequest(args[0], flags), null);
    }
}
