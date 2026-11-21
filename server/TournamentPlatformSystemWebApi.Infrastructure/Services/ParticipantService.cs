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

public class ParticipantService : IParticipantService, IParticipantRoster
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IUserRepository _userRepository;
    private readonly TournamentdbContext _context;
    private readonly ITournamentStatusResolver _statusResolver;

    public ParticipantService(ITournamentRepository tournamentRepository, IUserRepository userRepository, TournamentdbContext context, ITournamentStatusResolver statusResolver)
    {
        _tournamentRepository = tournamentRepository;
        _userRepository = userRepository;
        _context = context;
        _statusResolver = statusResolver;
    }

    public Task<int> CountActiveAsync(Guid tournamentId) =>
        _tournamentRepository.GetParticipantsCountAsync(tournamentId, includeDisqualified: false);

    public async Task<TournamentPlatformSystemWebApi.Application.DTOs.TeamDto> AddParticipantAsync(Guid tournamentId, Guid userId, Guid actorId)
    {
        var existing = await _statusResolver.GetTournamentWithRefreshedStatusAsync(tournamentId);
        if (existing == null) throw new KeyNotFoundException("Tournament not found");

        // Only allow adding participants while registration is open
        if (existing.Status != TournamentPlatformSystemWebApi.Core.Entities.TournamentStatus.REGISTRATION_OPEN)
            throw new TournamentPlatformSystemWebApi.Common.Exceptions.TournamentClosedForChangesException("Registration for tournament is already finished");

        // check user exists
        var user = await _userRepository.GetUserWithDetails(userId);
        if (user == null) throw new KeyNotFoundException("User not found");

        // Only the organizer can add other users; users may add themselves
        if (actorId != userId && existing.OrganizerId != actorId)
            throw new UnauthorizedAccessException("Only the tournament organizer can add other participants");
        // user must be a player (not organizer)
        if (user.IsOrganizer == true) throw new UnauthorizedAccessException("User does not have player role");

        // derive team name from user details
        var displayName = user.FullName?.Trim();
        var emailName = user.UserDetail?.Email?.Trim();
        var teamName = !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : !string.IsNullOrWhiteSpace(emailName)
                ? emailName
                : $"player_{userId.ToString().Substring(0, 8)}";

        if (await _tournamentRepository.IsTeamNameUsedAsync(tournamentId, teamName))
        {
            if (!string.IsNullOrWhiteSpace(emailName) && !string.Equals(teamName, emailName, StringComparison.InvariantCultureIgnoreCase))
            {
                teamName = emailName;
            }
            else
            {
                teamName = $"player_{userId.ToString().Substring(0, 8)}";
            }
        }

        if (await _tournamentRepository.IsTeamNameUsedAsync(tournamentId, teamName))
        {
            teamName = $"player_{userId.ToString().Substring(0, 8)}";
        }

        var provider = _context.Database.ProviderName ?? string.Empty;
        var useTransaction = _context.Database.CurrentTransaction == null && !provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase);
        await using var txn = useTransaction ? await _context.Database.BeginTransactionAsync() : null;

        try
        {
            var tournamentLock = await _context.Set<TournamentModel>()
                .FromSqlInterpolated($"SELECT * FROM tournament WHERE id = {tournamentId} FOR UPDATE")
                .FirstOrDefaultAsync();

            if (tournamentLock == null)
                throw new KeyNotFoundException("Tournament not found");

            if (tournamentLock.Status == TournamentStatusType.REGISTRATION_OPEN
                && tournamentLock.RegistrationDeadline != default
                && tournamentLock.RegistrationDeadline <= DateTime.UtcNow)
            {
                tournamentLock.Status = TournamentStatusType.REGISTRATION_CLOSED;
                tournamentLock.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                _context.Set<TournamentModel>().Update(tournamentLock);
                await _context.SaveChangesAsync();
                throw new TournamentPlatformSystemWebApi.Common.Exceptions.TournamentClosedForChangesException("Registration for tournament is already finished");
            }

            if (tournamentLock.Status != TournamentStatusType.REGISTRATION_OPEN)
                throw new TournamentPlatformSystemWebApi.Common.Exceptions.TournamentClosedForChangesException("Registration for tournament is already finished");

            var participantsCount = await CountActiveAsync(tournamentId);
            if (existing.MaxTeams > 0 && participantsCount >= existing.MaxTeams)
                throw new TournamentPlatformSystemWebApi.Common.Exceptions.MaxParticipantsReachedException("Maximum amount of participants is achieved, there is no spot for you");

            var already = await _tournamentRepository.IsUserInTournamentAsync(tournamentId, userId);
            if (already)
                throw new TournamentPlatformSystemWebApi.Common.Exceptions.ParticipantAlreadyAddedException("You already joined to this tournament");

            TournamentPlatformSystemWebApi.Core.Entities.Team team;
            try
            {
                team = await _tournamentRepository.AddParticipantAsync(tournamentId, userId, teamName);
            }
            catch (DbUpdateException dbEx)
            {
                if (dbEx.InnerException?.Message?.Contains("unique_user_tournament", StringComparison.InvariantCultureIgnoreCase) == true)
                {
                    throw new TournamentPlatformSystemWebApi.Common.Exceptions.ParticipantAlreadyAddedException("You already joined to this tournament");
                }

                throw;
            }

            if (useTransaction && txn != null)
            {
                await txn.CommitAsync();
            }

            return new TournamentPlatformSystemWebApi.Application.DTOs.TeamDto
            {
                Id = team.Id,
                Name = team.Name,
                UserId = userId,
                TournamentId = team.TournamentId,
                IsDisqualified = team.IsDisqualified,
                CreatedAt = team.CreatedAt,
                UpdatedAt = team.UpdatedAt
            };
        }
        catch
        {
            if (useTransaction && txn != null)
            {
                await txn.RollbackAsync();
            }

            throw;
        }
    }

    public async Task RemoveParticipantAsync(Guid tournamentId, Guid userId)
    {
        var existing = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (existing == null) throw new KeyNotFoundException("Tournament not found");

        if (existing.RegistrationDeadline != default && existing.RegistrationDeadline <= DateTime.UtcNow)
            throw new TournamentPlatformSystemWebApi.Common.Exceptions.TournamentClosedForChangesException("Registration is already closed");

        if (existing.Status == TournamentPlatformSystemWebApi.Core.Entities.TournamentStatus.IN_PROGRESS
            || existing.Status == TournamentPlatformSystemWebApi.Core.Entities.TournamentStatus.COMPLETED
            || existing.Status == TournamentPlatformSystemWebApi.Core.Entities.TournamentStatus.REGISTRATION_CLOSED)
        {
            throw new TournamentPlatformSystemWebApi.Common.Exceptions.TournamentClosedForChangesException("Tournament is closed for changes");
        }

        var matches = await _tournamentRepository.GetMatchesAsync(tournamentId);
        if (matches.Any())
            throw new TournamentPlatformSystemWebApi.Common.Exceptions.TournamentClosedForChangesException("Tournament bracket has already been generated");

        var isIn = await _tournamentRepository.IsUserInTournamentAsync(tournamentId, userId);
        if (!isIn)
            throw new TournamentPlatformSystemWebApi.Common.Exceptions.ParticipantNotFoundException("User is not a participant of this tournament");

        var success = await _tournamentRepository.RemoveParticipantAsync(tournamentId, userId);
        if (!success)
            throw new Exception("Failed to remove participant from tournament");
    }

    public async Task DisqualifyParticipantAsync(Guid tournamentId, Guid userId, Guid actorId)
    {
        var existing = await _statusResolver.GetTournamentWithRefreshedStatusAsync(tournamentId);

        // disallow if registration closed, in progress, or completed
        if (existing.Status == TournamentPlatformSystemWebApi.Core.Entities.TournamentStatus.REGISTRATION_CLOSED
            || existing.Status == TournamentPlatformSystemWebApi.Core.Entities.TournamentStatus.IN_PROGRESS
            || existing.Status == TournamentPlatformSystemWebApi.Core.Entities.TournamentStatus.COMPLETED)
        {
            throw new TournamentPlatformSystemWebApi.Common.Exceptions.TournamentClosedForChangesException("Tournament is closed for changes");
        }

        // check participant exists
        var isIn = await _tournamentRepository.IsUserInTournamentAsync(tournamentId, userId);
        if (!isIn) throw new KeyNotFoundException("Participant not found in tournament");

        // Only the organizer can disqualify participants
        if (existing.OrganizerId != actorId)
            throw new UnauthorizedAccessException("Only the tournament organizer can disqualify participants");

        var success = await _tournamentRepository.DisqualifyParticipantAsync(tournamentId, userId);
        if (!success)
            throw new Exception("Failed to disqualify participant");
    }

    public async Task<IReadOnlyList<TournamentPlatformSystemWebApi.Application.DTOs.TeamDto>> GetTournamentParticipantsAsync(Guid tournamentId)
    {
        var existing = await _statusResolver.GetTournamentWithRefreshedStatusAsync(tournamentId);
        var teams = await _tournamentRepository.GetTeamsAsync(tournamentId);

        var dtos = teams.Select(t => new TournamentPlatformSystemWebApi.Application.DTOs.TeamDto
        {
            Id = t.Id,
            Name = t.Name,
            UserId = t.Participants.FirstOrDefault()?.UserId ?? Guid.Empty,
            TournamentId = t.TournamentId,
            IsDisqualified = t.IsDisqualified,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList().AsReadOnly();

        return dtos;
    }
}