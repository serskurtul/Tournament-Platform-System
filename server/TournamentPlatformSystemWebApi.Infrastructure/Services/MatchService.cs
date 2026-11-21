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

public class MatchService : IMatchService
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly TournamentdbContext _context;
    private readonly ITournamentStatusResolver _statusResolver;
    private readonly IParticipantRoster _participantRoster;
    private readonly TournamentPlatformSystemWebApi.Core.Logic.IBracketGenerator _bracketGenerator;
    private readonly TournamentPlatformSystemWebApi.Core.Logic.ISeedingStrategy _seedingStrategy;

    public MatchService(ITournamentRepository tournamentRepository, TournamentdbContext context, ITournamentStatusResolver statusResolver, IParticipantRoster participantRoster, TournamentPlatformSystemWebApi.Core.Logic.IBracketGenerator bracketGenerator, TournamentPlatformSystemWebApi.Core.Logic.ISeedingStrategy seedingStrategy)
    {
        _tournamentRepository = tournamentRepository;
        _context = context;
        _statusResolver = statusResolver;
        _participantRoster = participantRoster;
        _bracketGenerator = bracketGenerator;
        _seedingStrategy = seedingStrategy;
    }

    public async Task<TournamentPlatformSystemWebApi.Application.DTOs.TournamentStartResponse> StartTournament(Guid tournamentId, Guid organizerId)
    {
        var existing = await _statusResolver.GetTournamentWithRefreshedStatusAsync(tournamentId);
        if (existing == null) throw new KeyNotFoundException("Tournament not found");
        if (existing.OrganizerId != organizerId) throw new UnauthorizedAccessException("Not the organizer");
        if (existing.Status == TournamentStatus.IN_PROGRESS)
            throw new TournamentPlatformSystemWebApi.Common.Exceptions.TournamentAlreadyStartedException("Tournament is already started");
        if (existing.Status != TournamentStatus.REGISTRATION_CLOSED)
            throw new TournamentPlatformSystemWebApi.Common.Exceptions.TournamentClosedForChangesException("Tournament is not in registration_closed status");

        var participantsCount = await _participantRoster.CountActiveAsync(tournamentId);
        if (participantsCount < 2)
            throw new TournamentPlatformSystemWebApi.Common.Exceptions.InsufficientParticipantsException("At least 2 participants are required to start the tournament");

        // load teams and build bracket participants
        var teams = await _tournamentRepository.GetTeamsAsync(tournamentId);

        var participants = teams.Select(t => new TournamentPlatformSystemWebApi.Core.Logic.BracketParticipant
        {
            Id = t.Id,
            Name = t.Name
        }).ToList().AsReadOnly();

        // Bracket generator and seeding strategy are injected (single elimination + random seeding, see Program.cs)
        var matches = _bracketGenerator.Generate(tournamentId, participants, _seedingStrategy);

        // assign statuses and bye flags (important before persisting)
        TournamentPlatformSystemWebApi.Core.Logic.MatchStatusHelper.AssignStatuses(matches);

        var provider = _context.Database.ProviderName ?? string.Empty;
        var useTransaction = _context.Database.CurrentTransaction == null && !provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase);

        await using var txn = useTransaction ? await _context.Database.BeginTransactionAsync() : null;
        try
        {
            // persist generated matches
            await _tournamentRepository.AddMatchesAsync(matches);

            // update tournament status to IN_PROGRESS
            await _tournamentRepository.UpdateStatus(tournamentId, TournamentStatus.IN_PROGRESS);

            if (useTransaction && txn != null)
            {
                await txn.CommitAsync();
            }

            // return minimal start response
            var response = new TournamentPlatformSystemWebApi.Application.DTOs.TournamentStartResponse
            {
                TournamentId = tournamentId,
                Status = TournamentStatus.IN_PROGRESS.ToString().ToLowerInvariant(),
                MatchesCreated = matches?.Count ?? 0
            };

            return response;
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

    public async Task<IReadOnlyList<TournamentPlatformSystemWebApi.Application.DTOs.MatchesRoundDto>> GetTournamentMatchesAsync(Guid tournamentId)
    {
        var matches = await _tournamentRepository.GetMatchesAsync(tournamentId);

        var totalRounds = matches.Any() ? matches.Max(m => m.Level) : 0;

        var grouped = matches
            .GroupBy(m => m.Level)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var round = g.Key;
                var remaining = totalRounds - round + 1; // 1 => final

                string display;
                if (remaining == 1) display = "final";
                else if (remaining == 2) display = "semifinal";
                else if (remaining == 3) display = "quarterfinal";
                else display = $"1/{(1 << remaining)}";

                return new TournamentPlatformSystemWebApi.Application.DTOs.MatchesRoundDto
                {
                    Round = round,
                    RoundDisplayName = display,
                    Matches = g.Select(m => new TournamentPlatformSystemWebApi.Application.DTOs.MatchDto
                    {
                        MatchId = m.Id,
                        TournamentId = m.TournamentId,
                        Round = m.Level,
                        OrderNumber = m.OrderNumber,
                        Player1Id = m.TeamAId,
                        Player2Id = m.TeamBId,
                        Player1Name = m.TeamAName,
                        Player2Name = m.TeamBName,
                        Status = m.Status,
                        IsBye = m.IsBye,
                        ScorePlayer1 = m.TeamAScore,
                        ScorePlayer2 = m.TeamBScore,
                        WinnerId = m.WinnerId
                    }).ToList().AsReadOnly(),
                    MatchesCount = g.Count(),
                    NotByeMatchesCount = g.Count(x => x.TeamBId != null && x.TeamBId != Guid.Empty)
                };
            }).ToList().AsReadOnly();

        return grouped;
    }

    public async Task<TournamentPlatformSystemWebApi.Application.DTOs.MatchDto> SaveMatchResultAsync(Guid tournamentId, Guid matchId, MatchUpdateDto dto, Guid organizerId)
    {
        if (dto == null)
            throw new ValidationException("Match result data is required");

        if (dto.ScorePlayer1 < 0 || dto.ScorePlayer2 < 0)
            throw new ValidationException("Scores must be non-negative");

        if (dto.ScorePlayer1 == dto.ScorePlayer2)
            throw new ValidationException("Match cannot end in a draw");

        var existingTournament = await _statusResolver.GetTournamentWithRefreshedStatusAsync(tournamentId);
        if (existingTournament == null)
            throw new KeyNotFoundException("Tournament not found");

        if (existingTournament.OrganizerId != organizerId)
            throw new UnauthorizedAccessException("Not the organizer");

        if (existingTournament.Status != TournamentStatus.IN_PROGRESS)
            throw new ValidationException("Tournament is not in progress");

        var matches = (await _tournamentRepository.GetMatchesAsync(tournamentId)).ToList();
        var match = matches.FirstOrDefault(m => m.Id == matchId);
        if (match == null)
            throw new MatchNotFoundException("Match not found");

        if (match.IsBye == true)
            throw new ValidationException("Cannot enter a result for a bye match");

        if (match.Status == "pending" || !match.TeamAId.HasValue || !match.TeamBId.HasValue || match.TeamAId == Guid.Empty || match.TeamBId == Guid.Empty)
            throw new MatchNotReadyException("Match is not ready for result entry");

        if (match.WinnerId != null)
            throw new MatchResultAlreadySavedException("Result for this match has already been saved");

        if (dto.WinnerId.HasValue && dto.WinnerId != match.TeamAId && dto.WinnerId != match.TeamBId)
            throw new ValidationException("WinnerId must be one of the participating teams");

        var computedWinner = dto.ScorePlayer1 > dto.ScorePlayer2 ? match.TeamAId : match.TeamBId;
        dto.WinnerId ??= computedWinner;

        if (dto.WinnerId != computedWinner)
            throw new ValidationException("WinnerId does not match the submitted score");

        match.TeamAScore = dto.ScorePlayer1;
        match.TeamBScore = dto.ScorePlayer2;
        match.WinnerId = dto.WinnerId;
        match.Status = "completed";

        var nextLevel = match.Level + 1;
        TournamentPlatformSystemWebApi.Core.Entities.Match? nextMatch = null;

        var levelMatches = matches.Where(m => m.Level == match.Level).OrderBy(m => m.OrderNumber).ToList();
        var currentIndex = levelMatches.FindIndex(m => m.Id == matchId);
        if (currentIndex >= 0)
        {
            var nextLevelMatches = matches.Where(m => m.Level == nextLevel).OrderBy(m => m.OrderNumber).ToList();
            if (nextLevelMatches.Any())
            {
                var nextIndex = currentIndex / 2;
                if (nextIndex < nextLevelMatches.Count)
                {
                    nextMatch = nextLevelMatches[nextIndex];
                    if (currentIndex % 2 == 0)
                    {
                        nextMatch.TeamAId = match.WinnerId;
                        nextMatch.TeamAName = match.TeamAId == match.WinnerId ? match.TeamAName : match.TeamBName;
                    }
                    else
                    {
                        nextMatch.TeamBId = match.WinnerId;
                        nextMatch.TeamBName = match.TeamAId == match.WinnerId ? match.TeamAName : match.TeamBName;
                    }

                    nextMatch.Status = nextMatch.TeamAId.HasValue && nextMatch.TeamBId.HasValue
                        ? "scheduled"
                        : "pending";
                }
            }
        }

        match.Status = "completed";

        var provider = _context.Database.ProviderName ?? string.Empty;
        var useTransaction = !provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase);

        await using var txn = useTransaction ? await _context.Database.BeginTransactionAsync() : null;
        try
        {
            var updatedMatch = await _tournamentRepository.UpdateMatchAsync(match);
            if (nextMatch != null)
                await _tournamentRepository.UpdateMatchAsync(nextMatch);

            var maxLevel = matches.Any() ? matches.Max(m => m.Level) : 0;
            if (match.Level == maxLevel)
            {
                await _tournamentRepository.UpdateStatus(tournamentId, TournamentStatus.COMPLETED);
            }

            if (useTransaction && txn != null)
            {
                await txn.CommitAsync();
            }

            return new TournamentPlatformSystemWebApi.Application.DTOs.MatchDto
            {
                MatchId = updatedMatch.Id,
                TournamentId = updatedMatch.TournamentId,
                Round = updatedMatch.Level,
                OrderNumber = updatedMatch.OrderNumber,
                Player1Id = updatedMatch.TeamAId,
                Player2Id = updatedMatch.TeamBId,
                Player1Name = updatedMatch.TeamAName,
                Player2Name = updatedMatch.TeamBName,
                Status = updatedMatch.Status,
                IsBye = updatedMatch.IsBye,
                ScorePlayer1 = updatedMatch.TeamAScore,
                ScorePlayer2 = updatedMatch.TeamBScore,
                WinnerId = updatedMatch.WinnerId
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
}