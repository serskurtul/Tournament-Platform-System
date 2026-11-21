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

public class TournamentCatalogService : ITournamentCatalogService
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly TournamentdbContext _context;

    public TournamentCatalogService(ITournamentRepository tournamentRepository, TournamentdbContext context)
    {
        _tournamentRepository = tournamentRepository;
        _context = context;
    }

    public async Task<IReadOnlyList<TournamentPreviewDto>> GetTournamentsForUserAsync(Guid userId, int page, int pageSize, IReadOnlyList<TournamentStatus>? statuses)
    {
        if (page < 1)
            throw new ValidationException("Page must be greater than or equal to 1");

        if (pageSize < 1)
            throw new ValidationException("PageSize must be greater than or equal to 1");

        return await _tournamentRepository.GetForUserAsync(userId, page, pageSize, statuses);
    }

    public async Task<IReadOnlyList<TournamentPreviewDto>> GetAllTournamentsAsync(int page, int pageSize, bool randomize, IReadOnlyList<TournamentStatus>? statuses, string? query = null)
    {
        if (page < 1)
            throw new ValidationException("Page must be greater than or equal to 1");

        if (pageSize < 1)
            throw new ValidationException("PageSize must be greater than or equal to 1");

        return await _tournamentRepository.GetAllPreviewAsync(page, pageSize, randomize, statuses, query);
    }

    public async Task<IReadOnlyList<Guid>> GetUserPreferredThemeIdsAsync(Guid userId)
    {
        if (userId == Guid.Empty)
            return Array.Empty<Guid>();

        return await _context.Set<UserTournamentThemePreferenceModel>()
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ThemeId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TournamentPreviewDto>> GetPersonalizedTournamentsAsync(IReadOnlyCollection<Guid> preferredThemeIds, int page, int pageSize, bool randomize, IReadOnlyList<TournamentStatus>? statuses, string? query = null)
    {
        if (preferredThemeIds == null || preferredThemeIds.Count == 0)
            return Array.Empty<TournamentPreviewDto>();

        if (page < 1)
            throw new ValidationException("Page must be greater than or equal to 1");

        if (pageSize < 1)
            throw new ValidationException("PageSize must be greater than or equal to 1");

        var skip = (page - 1) * pageSize;

        var statusFilters = statuses != null && statuses.Count > 0
            ? statuses.Select(s => (TournamentStatusType)s).ToList()
            : new List<TournamentStatusType> { TournamentStatusType.REGISTRATION_OPEN, TournamentStatusType.IN_PROGRESS };

        var searchQuery = query;
        var tournamentsQuery = _context.Set<TournamentModel>()
            .AsNoTracking()
            .Where(t => preferredThemeIds.Contains(t.ThemeId));

        if (statusFilters != null)
            tournamentsQuery = tournamentsQuery.Where(t => statusFilters.Contains(t.Status));

        tournamentsQuery = ApplySearchQuery(tournamentsQuery, searchQuery);
        tournamentsQuery = string.IsNullOrWhiteSpace(searchQuery)
            ? (randomize
                ? tournamentsQuery.OrderBy(_ => EF.Functions.Random())
                : tournamentsQuery.OrderByDescending(t => t.StartDate).ThenBy(t => t.Id))
            : ApplySearchRanking(tournamentsQuery, searchQuery, randomize);

        var rows = await tournamentsQuery
            .Skip(skip)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                Title = t.Name,
                t.Status,
                t.BackgroundImg,
                SportName = t.Theme != null ? t.Theme.Name : null,
                t.StartDate,
                t.MaxTeams,
                ParticipantsCount = t.Teams.Count(team => team.IsDisqualified == null || team.IsDisqualified == false)
            })
            .ToListAsync();

        return rows.Select(r => new TournamentPreviewDto
        {
            Id = r.Id,
            Title = r.Title,
            Status = r.Status.ToString().ToLowerInvariant(),
            BackgroundImg = r.BackgroundImg,
            SportName = r.SportName,
            StartDate = r.StartDate,
            ParticipantsCount = r.ParticipantsCount,
            MaxParticipants = r.MaxTeams
        }).ToList();
    }

    private static IQueryable<TournamentModel> ApplySearchQuery(IQueryable<TournamentModel> query, string? searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
            return query;

        var normalized = searchQuery.Trim().ToLowerInvariant();
        return query.Where(t =>
            t.Name.ToLower().Contains(normalized) ||
            (t.Description != null && t.Description.ToLower().Contains(normalized)));
    }

    private static IOrderedQueryable<TournamentModel> ApplySearchRanking(IQueryable<TournamentModel> query, string searchQuery, bool randomize)
    {
        var normalized = searchQuery.Trim().ToLowerInvariant();
        var tokens = searchQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var ordered = query
            .OrderByDescending(t => t.Name.ToLower().Contains(normalized))
            .ThenByDescending(t => t.Description != null && t.Description.ToLower().Contains(normalized))
            .ThenByDescending(BuildContainsAnyExpression(tokens, t => t.Name))
            .ThenByDescending(BuildContainsAnyExpression(tokens, t => t.Description));

        if (randomize)
            ordered = ordered.ThenBy(_ => EF.Functions.Random());
        else
            ordered = ordered.ThenByDescending(t => t.StartDate).ThenBy(t => t.Id);

        return ordered;
    }

    private static Expression<Func<TournamentModel, bool>> BuildContainsAnyExpression(string[] tokens, Expression<Func<TournamentModel, string?>> propertySelector)
    {
        var parameter = propertySelector.Parameters[0];
        var property = propertySelector.Body;
        var stringType = typeof(string);
        var toLowerMethod = stringType.GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
        var containsMethod = stringType.GetMethod(nameof(string.Contains), new[] { stringType })!;

        Expression body = Expression.Constant(false);
        var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));

        foreach (var token in tokens)
        {
            var lowered = Expression.Call(property, toLowerMethod);
            var containsCall = Expression.Call(lowered, containsMethod, Expression.Constant(token));
            var condition = Expression.AndAlso(notNull, containsCall);
            body = Expression.OrElse(body, condition);
        }

        return Expression.Lambda<Func<TournamentModel, bool>>(body, parameter);
    }
}