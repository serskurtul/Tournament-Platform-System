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

public class TournamentDetailsService : ITournamentDetailsService
{
    private readonly ITournamentStatusResolver _statusResolver;
    private readonly IParticipantRoster _participantRoster;

    public TournamentDetailsService(ITournamentStatusResolver statusResolver, IParticipantRoster participantRoster)
    {
        _statusResolver = statusResolver;
        _participantRoster = participantRoster;
    }

    public async Task<TournamentPlatformSystemWebApi.Application.DTOs.TournamentDetailsDto> GetTournamentDetailsAsync(Guid tournamentId)
    {
        var existing = await _statusResolver.GetTournamentWithRefreshedStatusAsync(tournamentId);
        var participantsCount = await _participantRoster.CountActiveAsync(tournamentId);


        var result = new TournamentPlatformSystemWebApi.Application.DTOs.TournamentDetailsDto
        {
            Id = existing.Id,
            Title = existing.Name,
            Description = existing.Description,
            Conditions = existing.Conditions,
            StartDate = existing.StartDate,
            EndDate = existing.EndDate,
            RegistrationCloseDate = existing.RegistrationDeadline,
            SportId = existing.ThemeId,
            SportName = existing.ThemeName,
            MaxParticipants = existing.MaxTeams,
            Status = existing.Status.ToString().ToLowerInvariant(),
            OrganizerId = existing.OrganizerId ?? Guid.Empty,
            OrganizerName = existing.OrganizerName ?? string.Empty,
            BackgroundImg = existing.BackgroundImg,
            ParticipantsCount = participantsCount
        };

        return result;
    }

    public async Task<IReadOnlyList<TournamentPlatformSystemWebApi.Application.DTOs.EventDto>> GetTournamentEventsAsync(Guid tournamentId)
    {
        var existing = await _statusResolver.GetTournamentWithRefreshedStatusAsync(tournamentId);

        var events = new List<TournamentPlatformSystemWebApi.Application.DTOs.EventDto>();

        // Created event
        events.Add(new TournamentPlatformSystemWebApi.Application.DTOs.EventDto
        {
            Id = Guid.NewGuid(),
            Type = "tournament_created",
            Message = "Tournament created",
            CreatedAt = existing.CreatedAt ?? DateTime.UtcNow
        });

        // Registration closed event if registration deadline passed or status indicates closed
        if (existing.RegistrationDeadline != default && (existing.RegistrationDeadline <= DateTime.UtcNow || existing.Status == TournamentPlatformSystemWebApi.Core.Entities.TournamentStatus.REGISTRATION_CLOSED))
        {
            events.Add(new TournamentPlatformSystemWebApi.Application.DTOs.EventDto
            {
                Id = Guid.NewGuid(),
                Type = "registration_closed",
                Message = "Registration closed",
                CreatedAt = existing.RegistrationDeadline
            });
        }

        // Tournament started event when status is in progress
        if (existing.Status == TournamentPlatformSystemWebApi.Core.Entities.TournamentStatus.IN_PROGRESS)
        {
            events.Add(new TournamentPlatformSystemWebApi.Application.DTOs.EventDto
            {
                Id = Guid.NewGuid(),
                Type = "tournament_started",
                Message = "Tournament started",
                CreatedAt = existing.StartDate
            });
        }

        // Tournament completed event when status is completed
        if (existing.Status == TournamentPlatformSystemWebApi.Core.Entities.TournamentStatus.COMPLETED)
        {
            events.Add(new TournamentPlatformSystemWebApi.Application.DTOs.EventDto
            {
                Id = Guid.NewGuid(),
                Type = "tournament_completed",
                Message = "Tournament completed; winners determined",
                CreatedAt = existing.EndDate != default ? existing.EndDate : DateTime.UtcNow
            });
        }

        return events.AsReadOnly();
    }
}