namespace Romp.BuildingBlocks.Persistence.Outbox;

/// <summary>
/// A module's one-time opt-in to the shared platform outbox dispatcher (SCRUM-181), made from its
/// own <c>RegisterServices</c> via <see cref="OutboxServiceCollectionExtensions.AddOutboxModule"/>.
/// Names the module's own schema (its <c>OUTB_MSG</c> table, per
/// <see cref="OutboxModelBuilderExtensions.HasOutboxTable"/>) and every domain event CLR type its
/// own <see cref="OutboxSaveChangesInterceptor"/> may write there - the dispatcher needs the type
/// to deserialize a row's JSON payload and to resolve the matching
/// <see cref="IOutboxMessageHandler{TEvent}"/>, without ever depending on the module's own
/// Domain/Application/Infrastructure assembly (ADR 0002 - the dispatcher lives in
/// Romp.BuildingBlocks and stays module-agnostic).
/// </summary>
public sealed class OutboxModuleRegistration
{
    public OutboxModuleRegistration(string schema, IEnumerable<Type> eventTypes)
    {
        Schema = schema;
        EventTypesByName = eventTypes.ToDictionary(t => t.Name);
    }

    public string Schema { get; }

    public IReadOnlyDictionary<string, Type> EventTypesByName { get; }
}
