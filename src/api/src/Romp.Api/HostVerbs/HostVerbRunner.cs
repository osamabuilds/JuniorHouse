using Romp.BuildingBlocks.Persistence.Auditing;

namespace Romp.Api.HostVerbs;

public delegate Task<int> HostVerbHandler(HostVerbRequest request, TextWriter output, CancellationToken cancellationToken);

/// <summary>Runs one operator verb as <c>operator-cli</c> and turns the outcome into an exit code. Failures print only the exception type.</summary>
public static class HostVerbRunner
{
    public const string Actor = "operator-cli";
    public const int Failed = 1;
    public const int NotAvailable = 69;

    public static async Task<int> RunAsync(
        HostVerbRequest request,
        IReadOnlyDictionary<string, HostVerbHandler> handlers,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!handlers.TryGetValue(request.Verb, out var handler))
        {
            await error.WriteLineAsync($"Operator verb '{request.Verb}' is not available in this build yet.");
            return NotAvailable;
        }

        try
        {
            using (ActorScope.Begin(Actor))
            {
                return await handler(request, output, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await error.WriteLineAsync($"Operator verb '{request.Verb}' failed ({ex.GetType().Name}). Details are withheld from the console.");
            return Failed;
        }
    }
}
