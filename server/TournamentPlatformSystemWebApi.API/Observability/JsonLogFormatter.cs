using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace TournamentPlatformSystemWebApi.API.Observability;

/// <summary>
/// Writes one flat JSON object per log record to stdout:
/// {"time","level","category","message", ...structured properties as top-level fields}.
/// Flat fields let Loki/LogQL filter with `| json | status >= 500` without nested paths.
/// </summary>
public sealed class JsonLogFormatter : ConsoleFormatter
{
    // Must differ from the built-in "json" formatter name, otherwise the framework keeps its own.
    public const string FormatterName = "flatjson";

    public JsonLogFormatter() : base(FormatterName)
    {
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (string.IsNullOrEmpty(message) && logEntry.Exception is null)
        {
            return;
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteString("level", logEntry.LogLevel.ToString());
            writer.WriteString("category", logEntry.Category);
            writer.WriteString("message", message);

            if (logEntry.State is IEnumerable<KeyValuePair<string, object?>> properties)
            {
                foreach (var (key, value) in properties)
                {
                    if (key == "{OriginalFormat}")
                    {
                        continue;
                    }

                    WriteProperty(writer, ToCamelCase(key), value);
                }
            }

            if (logEntry.Exception is not null)
            {
                writer.WriteString("exception", logEntry.Exception.ToString());
            }

            writer.WriteEndObject();
        }

        textWriter.WriteLine(Encoding.UTF8.GetString(buffer.ToArray()));
    }

    private static void WriteProperty(Utf8JsonWriter writer, string name, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNull(name);
                break;
            case bool b:
                writer.WriteBoolean(name, b);
                break;
            case int i:
                writer.WriteNumber(name, i);
                break;
            case long l:
                writer.WriteNumber(name, l);
                break;
            case double d:
                writer.WriteNumber(name, d);
                break;
            case float f:
                writer.WriteNumber(name, f);
                break;
            case decimal m:
                writer.WriteNumber(name, m);
                break;
            default:
                writer.WriteString(name, value.ToString());
                break;
        }
    }

    private static string ToCamelCase(string key) =>
        string.IsNullOrEmpty(key) || char.IsLower(key[0]) ? key : char.ToLowerInvariant(key[0]) + key[1..];
}