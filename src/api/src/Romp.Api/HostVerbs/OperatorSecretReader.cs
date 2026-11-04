namespace Romp.Api.HostVerbs;

/// <summary>Reads a secret for an operator verb: the named environment variable, else a no-echo prompt. Never an argument.</summary>
public sealed class OperatorSecretReader(Func<string, string?> environment, Func<string, string?> prompt)
{
    public static OperatorSecretReader ForConsole() => new(Environment.GetEnvironmentVariable, ConsolePrompt);

    public string Read(string environmentVariable, string promptText)
    {
        var fromEnvironment = environment(environmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        var typed = prompt(promptText);
        if (!string.IsNullOrWhiteSpace(typed))
        {
            return typed;
        }

        throw new InvalidOperationException($"No secret supplied: set the {environmentVariable} environment variable or run interactively.");
    }

    private static string? ConsolePrompt(string text)
    {
        if (Console.IsInputRedirected)
        {
            return null;
        }

        Console.Error.Write(text);
        var buffer = new System.Text.StringBuilder();
        while (Console.ReadKey(intercept: true) is var key && key.Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
            }
        }

        Console.Error.WriteLine();
        return buffer.ToString();
    }
}
