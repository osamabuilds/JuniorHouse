namespace Romp.BuildingBlocks.Persistence.Auditing;

/// <summary>
/// The only <see cref="ICurrentActor"/> implementation until staff authentication (S8) exists -
/// every change is attributed to a fixed "system" actor rather than a real signed-in user, unless an <see cref="ActorScope"/> (an operator verb) names one.
/// </summary>
public sealed class SystemCurrentActor : ICurrentActor
{
    public string UserName => ActorScope.Current ?? "system";
}
