
namespace Romp.Modules.Vendor.Application.Files;

/// <summary>
/// SCRUM-93 task 35 (AC-45). Detects an allowed file type from its leading bytes (its content
/// signature) - never from the client-supplied filename or content type, both of which a caller
/// controls. Allowed: PDF, PNG, JPEG, and ZIP-based Office files (xlsx/docx). Also makes a filename
/// safe to display and to send in a Content-Disposition header (AC-46).
/// </summary>
public static class PoFileContent
{
    public const string AllowedTypesDescription = "PDF, PNG, JPEG, Excel (.xlsx) or Word (.docx)";

    public static string? DetectContentType(byte[] content)
    {
        if (StartsWith(content, "%PDF-"u8))
        {
            return "application/pdf";
        }

        if (StartsWith(content, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        if (StartsWith(content, [0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        if (StartsWith(content, [0x50, 0x4B, 0x03, 0x04]))
        {
            return "application/octet-stream"; // Office Open XML container - offered only as a download
        }

        return null;
    }

    /// <summary>Strips any path, control characters and header-breaking characters; never returns empty.</summary>
    public static string SanitiseFileName(string fileName)
    {
        var name = fileName.Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];

        var cleaned = new string(name
            .Where(c => !char.IsControl(c) && c is not ('"' or ';' or '<' or '>' or ':' or '*' or '?' or '|'))
            .ToArray())
            .Trim().Trim('.');

        if (cleaned.Length > 200)
        {
            cleaned = cleaned[^200..];
        }

        return cleaned.Length == 0 ? "file" : cleaned;
    }

    private static bool StartsWith(byte[] content, ReadOnlySpan<byte> signature) =>
        content.Length >= signature.Length && content.AsSpan(0, signature.Length).SequenceEqual(signature);
}
