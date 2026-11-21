using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace TournamentPlatformSystemWebApi.Infrastructure.Security
{
    /// <summary>
    /// Immutable value object: who the access token is issued for.
    /// Groups the four loose method arguments and owns the mapping to JWT claims.
    /// </summary>
    public sealed record AccessTokenSubject(Guid UserId, string Email, string Role, bool IsOrganizer)
    {
        public IReadOnlyList<Claim> ToClaims(string jwtId) => new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, jwtId),
            new Claim(JwtRegisteredClaimNames.Email, Email),
            new Claim("role", Role),
            new Claim("isOrganizer", IsOrganizer.ToString())
        };
    }
}