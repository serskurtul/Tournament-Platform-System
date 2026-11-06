using System.Diagnostics;
using Prometheus;

namespace TournamentPlatformSystemWebApi.API.Middleware;

/// <summary>
/// Logs every request as one structured line and records Prometheus metrics.
/// Labels use the route TEMPLATE (e.g. api/v1/tournaments/{id}), never the raw path,
/// so cardinality stays bounded.
/// </summary>
public class RequestLoggingMiddleware
{
    private const string UnmatchedRoute = "unmatched";

    private static readonly Counter _httpRequestsTotal = Metrics.CreateCounter(
        "http_requests_total",
        "Total HTTP requests handled by the API.",
        new CounterConfiguration { LabelNames = new[] { "method", "route", "status" } });

    private static readonly Histogram _httpRequestDuration = Metrics.CreateHistogram(
        "http_request_duration_seconds",
        "HTTP request duration in seconds.",
        new HistogramConfiguration
        {
            LabelNames = new[] { "method", "route", "status" },
            Buckets = Histogram.ExponentialBuckets(0.005, 2, 10)
        });

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();

            var method = context.Request.Method;
            var route = (context.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText
                        ?? UnmatchedRoute;
            var status = context.Response.StatusCode.ToString();
            var durationMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);

            _httpRequestsTotal.WithLabels(method, route, status).Inc();
            _httpRequestDuration.WithLabels(method, route, status).Observe(sw.Elapsed.TotalSeconds);

            // Properties become top-level JSON fields in stdout (see JsonLogFormatter).
            // Field names in stdout JSON: method, route, status, durationMs (queried by LogQL in ЛР-7).
            _logger.LogInformation("HTTP {Method} {Route} responded {Status} in {DurationMs} ms",
                method, route, context.Response.StatusCode, durationMs);
        }
    }
}