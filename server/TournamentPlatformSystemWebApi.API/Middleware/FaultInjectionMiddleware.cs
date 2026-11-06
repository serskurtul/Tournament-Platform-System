using System.Globalization;

namespace TournamentPlatformSystemWebApi.API.Middleware;

/// <summary>
/// Demo-only fault injection for the observability / rollback lab.
/// When FAIL_RATE (0..1) is set, that fraction of /api/* requests returns HTTP 500.
/// Health, version and metrics endpoints are never affected. Default: disabled (0).
/// </summary>
public class FaultInjectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly double _failRate;

    public FaultInjectionMiddleware(RequestDelegate next)
    {
        _next = next;
        var raw = Environment.GetEnvironmentVariable("FAIL_RATE");
        _failRate = double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate)
            ? Math.Clamp(rate, 0d, 1d)
            : 0d;
    }

    public double FailRate => _failRate;

    public async Task InvokeAsync(HttpContext context)
    {
        if (_failRate > 0
            && context.Request.Path.StartsWithSegments("/api")
            && Random.Shared.NextDouble() < _failRate)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"error\":{\"code\":500,\"type\":\"InjectedFailure\",\"message\":\"Injected failure (FAIL_RATE)\"}}");
            return;
        }

        await _next(context);
    }
}