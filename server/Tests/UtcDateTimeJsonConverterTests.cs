using System;
using System.Text.Json;
using TournamentPlatformSystemWebApi.API.Serialization;
using Xunit;

namespace Tests
{
    public class UtcDateTimeJsonConverterTests
    {
        private static readonly JsonSerializerOptions Options = new() { Converters = { new UtcDateTimeJsonConverter() } };

        [Fact]
        public void Write_UnspecifiedKind_IsSerializedAsUtcWithZ()
        {
            var value = new DateTime(2026, 11, 21, 20, 14, 41, DateTimeKind.Unspecified);

            Assert.Equal("\"2026-11-21T20:14:41Z\"", JsonSerializer.Serialize(value, Options));
        }

        [Fact]
        public void Write_NullableDateTime_UsesConverter()
        {
            DateTime? value = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

            Assert.Equal("\"2026-01-02T03:04:05Z\"", JsonSerializer.Serialize(value, Options));
        }

        [Fact]
        public void Read_OffsetValue_IsConvertedToUtc()
        {
            var value = JsonSerializer.Deserialize<DateTime>("\"2026-11-21T22:00:00+02:00\"", Options);

            Assert.Equal(DateTimeKind.Utc, value.Kind);
            Assert.Equal(new DateTime(2026, 11, 21, 20, 0, 0, DateTimeKind.Utc), value);
        }
    }
}