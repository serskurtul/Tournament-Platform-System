
using TournamentPlatformSystemWebApi.API.Swagger;
using TournamentPlatformSystemWebApi.API.Middleware;
using Microsoft.AspNetCore.Mvc;
using TournamentPlatformSystemWebApi.API.Endpoints;
using TournamentPlatformSystemWebApi.Infrastructure.Services;
using TournamentPlatformSystemWebApi.Application.Interfaces;
using TournamentPlatformSystemWebApi.Common.Models;
using TournamentPlatformSystemWebApi.Infrastructure.Configurations;
using TournamentPlatformSystemWebApi.Infrastructure.Repositories;
using TournamentPlatformSystemWebApi.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Console;
using Prometheus;
using TournamentPlatformSystemWebApi.API.Observability;

var builder = WebApplication.CreateBuilder(args);

// Structured logs: one flat JSON object per line on stdout (consumed by Grafana Alloy -> Loki).
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.FormatterName = JsonLogFormatter.FormatterName);
builder.Logging.AddConsoleFormatter<JsonLogFormatter, ConsoleFormatterOptions>();

// Load optional secrets file from project root (useful in Docker or local mounts)
// The file should be named `tournament-platform-system-secrets.json` and contain valid JSON
// matching the expected configuration structure).
var secretsFile = "tournament-platform-system-secrets.json";
if (File.Exists(secretsFile))
{
    builder.Configuration.AddJsonFile(secretsFile, optional: true, reloadOnChange: false);
}
else
{
    System.Console.WriteLine("`tournament-platform-system-secrets.json` not provided");
    builder.Configuration.AddJsonFile(secretsFile, optional: true, reloadOnChange: false);
}

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new TournamentPlatformSystemWebApi.API.Serialization.UtcDateTimeJsonConverter()));

// Add in-memory cache for rate-limiting login attempts
builder.Services.AddMemoryCache();

// Return validation errors in unified ErrorResponseDto format
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var fieldErrors = context.ModelState
            .Where(x => x.Value != null && x.Value.Errors.Count > 0)
            .SelectMany(kvp => kvp.Value!.Errors.Select(e => new FieldError
            {
                Field = string.IsNullOrEmpty(kvp.Key) ? "" : char.ToLowerInvariant(kvp.Key[0]) + kvp.Key.Substring(1),
                Message = e.ErrorMessage
            }))
            .ToList();

        var errorDto = new ErrorResponseDto
        {
            Error = new ErrorDetail
            {
                Code = StatusCodes.Status400BadRequest,
                Type = "ValidationError",
                Message = "Validation failed",
                FieldErrors = fieldErrors,
                Path = context.HttpContext.Request.Path,
                Timestamp = DateTime.UtcNow.ToString("o"),
                TraceId = context.HttpContext.TraceIdentifier
            }
        };

        return new BadRequestObjectResult(errorDto);
    };
});
builder.Services.AddEndpointsApiExplorer();

// Configure CORS to allow requests from frontend origin (set via configuration or env var)
var frontendOrigin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN") ?? "http://localhost:5173";
// Development only: the documentation site (mkdocs serve / GitHub Pages) calls the local API from Swagger UI "Try it out".
var allowedOrigins = new List<string> { frontendOrigin };
if (builder.Environment.IsDevelopment())
{
    allowedOrigins.AddRange((Environment.GetEnvironmentVariable("DOCS_ORIGINS") ?? "http://127.0.0.1:8000,http://localhost:8000,https://serskurtul.github.io")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add db repositories and state checker
var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
// configure db
builder.Services.AddTournamentDb(dbConnectionString);

builder.Services.AddTransient<IDbStateChecker, DbStateChecker>(serviceProvider =>
{
    return new DbStateChecker(dbConnectionString);
});

// infra services
// Generic CRUD helper composed into every repository (ЛР-8: composition instead of inheritance).
builder.Services.AddScoped(typeof(BaseRepository<,>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserPreferencesService, UserPreferencesService>();
builder.Services.AddScoped<ITournamentRepository, TournamentRepository>();
builder.Services.AddScoped<IThemeRepository, ThemeRepository>();
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>(sp => new BcryptPasswordHasher(12));
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
// JWT token options and service
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtOptions = new JwtTokenOptions(
    jwtSection["Key"] ?? "default-development-key-change-in-production",
    jwtSection["Issuer"] ?? "tournament-api",
    int.TryParse(jwtSection["ExpirationMinutes"], out var m) ? m : 10
);

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>(sp => new JwtTokenService(jwtOptions));

// Register tournament service
// Tournament domain, decomposed by responsibility (ЛР-8). ParticipantService also owns IParticipantRoster.
builder.Services.AddScoped<ITournamentStatusResolver, TournamentStatusResolver>();
builder.Services.AddScoped<ParticipantService>();
builder.Services.AddScoped<IParticipantService>(sp => sp.GetRequiredService<ParticipantService>());
builder.Services.AddScoped<IParticipantRoster>(sp => sp.GetRequiredService<ParticipantService>());
builder.Services.AddScoped<ITournamentManagementService, TournamentManagementService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<ITournamentDetailsService, TournamentDetailsService>();
builder.Services.AddScoped<ITournamentCatalogService, TournamentCatalogService>();
builder.Services.AddTransient<TournamentPlatformSystemWebApi.Core.Logic.IBracketGenerator, TournamentPlatformSystemWebApi.Core.Logic.SingleEliminationBracketGenerator>();
builder.Services.AddTransient<TournamentPlatformSystemWebApi.Core.Logic.ISeedingStrategy, TournamentPlatformSystemWebApi.Core.Logic.RandomSeedingStrategy>();

// Register storage service (Google Drive)
builder.Services.AddSingleton<IStorageService, GoogleDriveStorageService>();

// Configure JWT authentication and set DefaultChallengeScheme
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var keyBytes = Encoding.UTF8.GetBytes(jwtOptions.Key ?? "default-development-key-change-in-production");
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Issuer ?? "tournament-api",
        ValidateAudience = true,
        ValidAudience = jwtOptions.Issuer ?? "tournament-api",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        // Use the "role" claim emitted by JwtTokenService for role checks
        RoleClaimType = ClaimTypes.Role,
        // Use subject as name identifier
        NameClaimType = JwtRegisteredClaimNames.Sub
    };

    //eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI4NzU2MDI1MC01N2UyLTQ4OTgtYWUxMi03OTU1ZTYwMWIxYmQiLCJlbWFpbCI6Im9yZ2FuaXplckBleGFtcGxlLmNvbSIsInJvbGUiOiJvcmdhbml6ZXIiLCJpc09yZ2FuaXplciI6IlRydWUiLCJleHAiOjE3Nzc5MjI1OTIsImlzcyI6InRvdXJuYW1lbnQtYXBpIiwiYXVkIjoidG91cm5hbWVudC1hcGkifQ._lfSPLKLpbs_L5PFEEuVt_CvwABmgdGnN459DS-jb7c
});

builder.Services.AddAuthorization();

// Swagger configuration moved to Swagger/SwaggerExtensions.cs
builder.Services.AddConfiguredSwagger();
// Register AutoMapper profiles from Infrastructure assembly
builder.Services.AddAutoMapper(_ => { }, typeof(TournamentPlatformSystemWebApi.Infrastructure.Mappings.UserProfile).Assembly);

var app = builder.Build();

// Register middlewares: logging -> exception handling -> response wrapping
app.UseRequestLogging();
app.UseFaultInjection();
app.UseExceptionHandling();
app.UseResponseWrapping();

// Use swagger middleware from extensions
app.UseConfiguredSwagger();

// Enable CORS using policy that allows the frontend origin
app.UseCors("FrontendPolicy");

// Health endpoint for liveness/readiness checks
app.MapGet("health", HealthHandler.HandleAsync);

// Application version (set by APP_VERSION env var; used by rollout verification in ЛР-5..7)
app.MapGet("version", () => Results.Ok(new { version = AppInfo.Version }));

// Prometheus scrape endpoint (no auth; exposed only inside the cluster)
app.MapMetrics("/metrics");

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();