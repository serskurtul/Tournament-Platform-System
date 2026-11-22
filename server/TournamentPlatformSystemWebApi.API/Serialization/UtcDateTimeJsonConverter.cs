using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TournamentPlatformSystemWebApi.API.Serialization
{
    /// <summary>
    /// Writes DateTime values as RFC 3339 UTC ("...Z"). Columns are "timestamp without time zone" holding UTC,
    /// so EF returns DateTimeKind.Unspecified and the default writer drops the offset; clients then read
    /// the value as local time. Found by the contract test (docs/api/openapi.yaml, format: date-time).
    /// </summary>
    public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetDateTime();
            return value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            var utc = value.Kind switch
            {
                DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => value,
            };
            writer.WriteStringValue(utc);
        }
    }
}