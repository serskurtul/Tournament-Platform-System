namespace TournamentPlatformSystemWebApi.API.Observability;

/// <summary>
/// Application version reported by /version and /health.
/// Set via the APP_VERSION environment variable (the image tag is injected there by the Helm chart).
/// </summary>
public static class AppInfo
{
    public static string Version { get; } =
        Environment.GetEnvironmentVariable("APP_VERSION") is { Length: > 0 } v ? v : "1.0.0";
}