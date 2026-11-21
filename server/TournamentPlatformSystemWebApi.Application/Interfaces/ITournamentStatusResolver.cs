using System;
using TournamentPlatformSystemWebApi.Application.DTOs;
using TournamentPlatformSystemWebApi.Core.Entities;

namespace TournamentPlatformSystemWebApi.Application.Interfaces;

/// <summary>Loads a tournament and closes registration if its deadline has passed (shared by all tournament services).</summary>
public interface ITournamentStatusResolver
{
    Task<Tournament> GetTournamentWithRefreshedStatusAsync(Guid tournamentId);
}