using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TournamentPlatformSystemWebApi.Application.Interfaces;
using TournamentPlatformSystemWebApi.Core.Entities;
using TournamentPlatformSystemWebApi.Infrastructure.Context;
using TournamentPlatformSystemWebApi.Infrastructure.Entities;

namespace TournamentPlatformSystemWebApi.Infrastructure.Repositories;

public class ThemeRepository : IThemeRepository
{
    // Composition instead of inheritance (ЛР-8): generic CRUD is delegated to a BaseRepository instance.
    private readonly TournamentdbContext _context;
    private readonly IMapper _mapper;
    private readonly BaseRepository<TournamentTheme, TournamentThemeModel> _crud;

    public ThemeRepository(TournamentdbContext context, IMapper mapper)
        : this(context, mapper, new BaseRepository<TournamentTheme, TournamentThemeModel>(context, mapper))
    {
    }

    public ThemeRepository(TournamentdbContext context, IMapper mapper, BaseRepository<TournamentTheme, TournamentThemeModel> crud)
    {
        _context = context;
        _mapper = mapper;
        _crud = crud;
    }

    public Task<ICollection<TournamentTheme>> GetAllAsync() => _crud.GetAllAsync();

    public Task<TournamentTheme?> GetByIdAsync(Guid id) => _crud.GetByIdAsync(id);

    public Task<Guid> CreateAsync(TournamentTheme entity) => _crud.CreateAsync(entity);

    public Task<TournamentTheme> UpdateAsync(TournamentTheme entity) => _crud.UpdateAsync(entity);

    public Task<bool> DeleteAsync(Guid id) => _crud.DeleteAsync(id);

    public async Task<bool> IsSportWithId(Guid id)
    {
        return await _context.Set<TournamentThemeModel>().AnyAsync(x => x.Id == id);
    }

    public async Task<bool> AreThemeIdsValidAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            return true;
        }

        var distinctIds = ids.Distinct().ToList();
        var count = await _context.Set<TournamentThemeModel>()
            .CountAsync(x => distinctIds.Contains(x.Id));

        return count == distinctIds.Count;
    }
}