using System;
using TournamentPlatformSystemWebApi.Application.DTOs;
using TournamentPlatformSystemWebApi.Core.Entities;

namespace TournamentPlatformSystemWebApi.Application.Interfaces;

/// <summary>Single source of truth for how many active (not disqualified) participants a tournament has.</summary>
public interface IParticipantRoster
{
    Task<int> CountActiveAsync(Guid tournamentId);
}