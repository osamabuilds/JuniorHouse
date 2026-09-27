namespace Romp.BuildingBlocks.Persistence;

/// <summary>
/// Identifies who is making the current change, for the INSR_BY/UPDT_BY audit columns. No
/// authentication exists yet (CLAUDE.md's Sprint 1 scope decision), so <see cref="SystemCurrentActor"/>
/// is a deliberate placeholder until SEC-08/S8 wires this to the authenticated caller.
/// </summary>
public interface ICurrentActor
{
    string UserName { get; }
}
