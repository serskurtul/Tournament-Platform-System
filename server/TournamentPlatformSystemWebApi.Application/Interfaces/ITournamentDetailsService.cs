using System;
using TournamentPlatformSystemWebApi.Application.DTOs;
using TournamentPlatformSystemWebApi.Core.Entities;

namespace TournamentPlatformSystemWebApi.Application.Interfaces;

/// <summary>Single-tournament read model: details and timeline of events.</summary>
public interface ITournamentDetailsService
{
    Task<TournamentPlatformSystemWebApi.Application.DTOs.TournamentDetailsDto> GetTournamentDetailsAsync(Guid tournamentId);
    Task<IReadOnlyList<TournamentPlatformSystemWebApi.Application.DTOs.EventDto>> GetTournamentEventsAsync(Guid tournamentId);
}