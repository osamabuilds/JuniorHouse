using System.Text.RegularExpressions;

namespace Romp.BuildingBlocks.Persistence.Auditing;

/// <summary>
/// Names the actor for everything done inside the scope, ahead of any signed-in user. Operator
/// verbs use it to record <c>operator-cli</c>. Flows with the async execution context.
/// </summary>
public static partial class ActorScope
{
    /// <summary>Longest name that fits INSR_BY / UPDT_BY.</summary>
    public const int MaxLength = 100;

    private static readonly AsyncLocal<string?> Slot = new();

    public static string? Current => Slot.Value;

    /// <summary>Actor names are restricted to <c>[a-z0-9._:-]</c> so they cannot forge log lines.</summary>
    public static bool IsValid(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= MaxLength && ActorNamePattern().IsMatch(name);

    public static IDisposable Begin(string actor)
    {
        if (!IsValid(actor))
        {
            throw new ArgumentException("Actor name must be 1 to 100 characters of [a-z0-9._:-].", nameof(actor));
        }

        var previous = Slot.Value;
        Slot.Value = actor;
        return new Restore(previous);
    }

    [GeneratedRegex("^[a-z0-9._:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ActorNamePattern();

    private sealed class Restore(string? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Slot.Value = previous;
        }
    }
}
