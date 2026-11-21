using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TournamentPlatformSystemWebApi.Application.DTOs;
using TournamentPlatformSystemWebApi.Application.Interfaces;
using TournamentPlatformSystemWebApi.Core.Entities;
using TournamentPlatformSystemWebApi.Infrastructure.Context;
using TournamentPlatformSystemWebApi.Infrastructure.Entities;
using System.ComponentModel.DataAnnotations;
using TournamentPlatformSystemWebApi.Common.Exceptions;

namespace TournamentPlatformSystemWebApi.Infrastructure.Services;

public class TournamentStatusResolver : ITournamentStatusResolver
{
    private readonly ITournamentRepository _tournamentRepository;

    public TournamentStatusResolver(ITournamentRepository tournamentRepository)
    {
        _tournamentRepository = tournamentRepository;
    }

    public async Task<Tournament> GetTournamentWithRefreshedStatusAsync(Guid tournamentId)
    {
        var existing = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (existing == null) throw new KeyNotFoundException("Tournament not found");

        if (existing.Status == TournamentStatus.REGISTRATION_OPEN
            && existing.RegistrationDeadline != default
            && existing.RegistrationDeadline <= DateTime.UtcNow)
        {
            await _tournamentRepository.UpdateStatus(existing.Id, TournamentStatus.REGISTRATION_CLOSED);
            existing.Status = TournamentStatus.REGISTRATION_CLOSED;
        }

        return existing;
    }
}