
namespace Romp.BuildingBlocks.Storage;

/// <summary>
/// SCRUM-93 task 34 (AC-45, plan.md). Storage keys are generated (never derived from the
/// client-supplied filename) - the original filename is kept only in <c>PO_FILE.FILE_NAME</c> for
/// display, never used to address the stored bytes. Local-disk is this sprint's implementation
/// (dev/Docker Compose); an S3-compatible one is a later, drop-in replacement behind this same
/// interface - module code never talks to a concrete storage SDK directly.
/// </summary>
public interface IFileStorage
{
    Task<string> SaveAsync(byte[] content, CancellationToken cancellationToken);

    Task<byte[]> RetrieveAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a stored file (Sprint 7, product images). Missing files are ignored. A default no-op so older storages and test doubles
    /// keep working; a leftover file is an orphan, never a data error.
    /// </summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.CompletedTask;
}
