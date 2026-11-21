using System;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TournamentPlatformSystemWebApi.Infrastructure.Security
{
    /// <summary>
    /// Immutable value object: resolved signing configuration (defaults applied once, at startup).
    /// Moves the configuration fallbacks (former control variables) out of the token-issuing path.
    /// </summary>
    public sealed class JwtSigningSettings
    {
        private const string DefaultKey = "default-development-key-change-in-production";
        private const string DefaultIssuer = "tournament-api";

        private static readonly JwtSecurityTokenHandler Handler = new();

        private JwtSigningSettings(string issuer, SigningCredentials credentials, TimeSpan lifetime)
        {
            Issuer = issuer;
            Credentials = credentials;
            Lifetime = lifetime;
        }

        public string Issuer { get; }

        public SigningCredentials Credentials { get; }

        public TimeSpan Lifetime { get; }

        public static JwtSigningSettings From(JwtTokenOptions options) => new(
            options.Issuer ?? DefaultIssuer,
            new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key ?? DefaultKey)),
                SecurityAlgorithms.HmacSha256),
            TimeSpan.FromMinutes(options.ExpirationMinutes));

        /// <summary>Signs a token for the subject; expiry is computed exactly once from <paramref name="now"/>.</summary>
        public IssuedAccessToken Issue(AccessTokenSubject subject, DateTimeOffset now) =>
            Sign(subject, new TokenEnvelope(Guid.NewGuid().ToString(), now.UtcDateTime + Lifetime));

        // The envelope is the single, immutable source of JwtId and ExpiresAt for both the token and the result.
        private IssuedAccessToken Sign(AccessTokenSubject subject, TokenEnvelope envelope) => new(
            Handler.WriteToken(new JwtSecurityToken(
                issuer: Issuer,
                audience: Issuer,
                claims: subject.ToClaims(envelope.JwtId),
                expires: envelope.ExpiresAt,
                signingCredentials: Credentials)),
            envelope.JwtId,
            envelope.ExpiresAt);

        private sealed record TokenEnvelope(string JwtId, DateTime ExpiresAt);
    }
}