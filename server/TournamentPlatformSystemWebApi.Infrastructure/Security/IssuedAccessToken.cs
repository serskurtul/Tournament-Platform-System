using System;

namespace TournamentPlatformSystemWebApi.Infrastructure.Security
{
    /// <summary>
    /// Immutable value object: a signed access token together with the exact expiry signed into it.
    /// ExpiresAt is set once at construction, so it cannot drift from the token's exp claim.
    /// </summary>
    public sealed record IssuedAccessToken(string Token, string JwtId, DateTime ExpiresAt);
}