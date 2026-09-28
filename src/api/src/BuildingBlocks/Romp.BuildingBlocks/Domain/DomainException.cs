namespace Romp.BuildingBlocks.Domain;

/// <summary>
/// A business-rule violation raised by an aggregate against its own current state - e.g. AC-13:
/// editing a PurchaseOrder that's already past Draft. Distinct from field-level input problems
/// (<c>Romp.BuildingBlocks.Application.ValidationException</c>); lives in the Domain-safe shared
/// kernel (not Application) so an aggregate can throw it without Domain depending on Application,
/// which would invert Clean Architecture's dependency rule. Romp.Api's DomainExceptionHandler maps
/// it to 409 Conflict.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
