using System;
using TournamentPlatformSystemWebApi.Application.DTOs;
using TournamentPlatformSystemWebApi.Core.Entities;

namespace TournamentPlatformSystemWebApi.Application.Interfaces;

/// <summary>Tournament catalog: listings, search and personalization.</summary>
public interface ITournamentCatalogService
{
    Task<IReadOnlyList<TournamentPreviewDto>> GetTournamentsForUserAsync(Guid userId, int page, int pageSize, IReadOnlyList<TournamentStatus>? statuses);
    Task<IReadOnlyList<TournamentPreviewDto>> GetAllTournamentsAsync(int page, int pageSize, bool randomize, IReadOnlyList<TournamentStatus>? statuses, string? query = null);
    Task<IReadOnlyList<Guid>> GetUserPreferredThemeIdsAsync(Guid userId);
    Task<IReadOnlyList<TournamentPreviewDto>> GetPersonalizedTournamentsAsync(IReadOnlyCollection<Guid> preferredThemeIds, int page, int pageSize, bool randomize, IReadOnlyList<TournamentStatus>? statuses, string? query = null);
}