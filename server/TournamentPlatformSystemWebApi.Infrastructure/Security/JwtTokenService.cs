using System;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace TournamentPlatformSystemWebApi.Infrastructure.Security
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtTokenOptions _options;
        private readonly JwtSigningSettings _signing;
        private readonly TimeProvider _clock;

        public JwtTokenService(JwtTokenOptions options)
            : this(options, TimeProvider.System)
        {
        }

        public JwtTokenService(JwtTokenOptions options, TimeProvider clock)
        {
            _options = options;
            _signing = JwtSigningSettings.From(options);
            _clock = clock;
        }

        public (string Token, string JwtId, DateTime ExpiresAt) GenerateToken(Guid userId, string email, string role, bool isOrganizer)
        {
            var issued = _signing.Issue(new AccessTokenSubject(userId, email, role, isOrganizer), _clock.GetUtcNow());
            return (issued.Token, issued.JwtId, issued.ExpiresAt);
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
            }
            // Use a Base64Url-encoded string that is safe for URLs, cookies, and headers.
            return Base64UrlEncoder.Encode(randomNumber);
        }

        public ClaimsPrincipal ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_options.Key ?? "default-development-key-change-in-production");

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer ?? "tournament-api",
                ValidateAudience = true,
                ValidAudience = _options.Issuer ?? "tournament-api",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            SecurityToken validatedToken;
            var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);
            return principal;
        }
    }
}