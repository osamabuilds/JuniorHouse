using System.Text.Json;
using System.Text.Json.Serialization;

namespace Romp.Modules.Identity.Domain.StaffAccounts;

/// <summary>
/// A password or other secret. <see cref="ToString"/> prints a mask and JSON serialisation throws, so logging or
/// returning one by mistake cannot leak it (staff-accounts AC-3, SEC-25). Only <see cref="Reveal"/> gives the value.
/// </summary>
[JsonConverter(typeof(SecretStringJsonConverter))]
public sealed class SecretString
{
    private readonly string _value;

    public SecretString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    public int Length => _value.Length;

    public string Reveal() => _value;

    public override string ToString() => "********";
}

internal sealed class SecretStringJsonConverter : JsonConverter<SecretString>
{
    public override SecretString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("A SecretString is never read from JSON.");

    public override void Write(Utf8JsonWriter writer, SecretString value, JsonSerializerOptions options) =>
        throw new NotSupportedException("A SecretString is never written to JSON.");
}
