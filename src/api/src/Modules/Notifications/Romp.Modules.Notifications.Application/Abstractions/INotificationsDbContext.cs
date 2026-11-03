namespace Romp.Modules.Notifications.Application.Abstractions;

/// <summary>
/// Application's port onto the <c>NTFY</c> database (ADR 0002): handlers depend on this, never on the Infrastructure project.
/// Sets are added here as the delivery, channels and delivery-log features arrive.
/// </summary>
public interface INotificationsDbContext
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
