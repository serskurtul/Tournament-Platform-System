using System;
using TournamentPlatformSystemWebApi.Application.DTOs;
using TournamentPlatformSystemWebApi.Core.Entities;

namespace TournamentPlatformSystemWebApi.Application.Interfaces;

/// <summary>Registering, removing and disqualifying tournament participants.</summary>
public interface IParticipantService
{
    Task<TournamentPlatformSystemWebApi.Application.DTOs.TeamDto> AddParticipantAsync(Guid tournamentId, Guid userId, Guid actorId);
    Task RemoveParticipantAsync(Guid tournamentId, Guid userId);
    Task DisqualifyParticipantAsync(Guid tournamentId, Guid userId, Guid actorId);
    Task<IReadOnlyList<TournamentPlatformSystemWebApi.Application.DTOs.TeamDto>> GetTournamentParticipantsAsync(Guid tournamentId);
}