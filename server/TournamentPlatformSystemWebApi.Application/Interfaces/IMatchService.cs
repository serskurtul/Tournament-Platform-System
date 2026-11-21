using System;
using TournamentPlatformSystemWebApi.Application.DTOs;
using TournamentPlatformSystemWebApi.Core.Entities;

namespace TournamentPlatformSystemWebApi.Application.Interfaces;

/// <summary>Starting a tournament, reading the bracket and saving match results.</summary>
public interface IMatchService
{
    Task<TournamentPlatformSystemWebApi.Application.DTOs.TournamentStartResponse> StartTournament(Guid tournamentId, Guid organizerId);
    Task<IReadOnlyList<TournamentPlatformSystemWebApi.Application.DTOs.MatchesRoundDto>> GetTournamentMatchesAsync(Guid tournamentId);
    Task<TournamentPlatformSystemWebApi.Application.DTOs.MatchDto> SaveMatchResultAsync(Guid tournamentId, Guid matchId, MatchUpdateDto dto, Guid organizerId);
}