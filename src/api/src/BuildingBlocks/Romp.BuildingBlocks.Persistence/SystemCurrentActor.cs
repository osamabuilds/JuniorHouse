namespace Romp.BuildingBlocks.Persistence;

/// <summary>
/// The only <see cref="ICurrentActor"/> implementation until staff authentication (S8) exists -
/// every change is attributed to a fixed "system" actor rather than a real signed-in user.
/// </summary>
public sealed class SystemCurrentActor : ICurrentActor
{
    public string UserName => "system";
}
