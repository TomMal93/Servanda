using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Servanda.Infrastructure.Data.Transfer;

/// <summary>
/// Ustawienia serializacji koperty eksportu; daty są zapisywane w UTC z sufiksem „Z”.
/// </summary>
internal static class TransferJson
{
    private const string UtcFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'";

    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        Converters = { new UtcDateTimeOffsetConverter() },
    };

    private sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            DateTimeOffset.Parse(
                reader.GetString() ?? string.Empty,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

        public override void Write(
            Utf8JsonWriter writer,
            DateTimeOffset value,
            JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToUniversalTime().ToString(UtcFormat, CultureInfo.InvariantCulture));
    }
}
