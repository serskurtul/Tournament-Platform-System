using System;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TournamentPlatformSystemWebApi.Application.Interfaces;
using TournamentPlatformSystemWebApi.Common.Helpers;
using TournamentPlatformSystemWebApi.Infrastructure.Context;
using TournamentPlatformSystemWebApi.Infrastructure.Entities;

namespace TournamentPlatformSystemWebApi.Infrastructure.Repositories;

public sealed class BaseRepository<TEntity, TDBEntity> : IRepository<TEntity, Guid> where TEntity : BaseEntity where TDBEntity : BaseDbEntity
{
    private readonly TournamentdbContext _context;
    private readonly IMapper _mapper;

    public BaseRepository(TournamentdbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<Guid> CreateAsync(TEntity entity)
    {
        var dbModel = _mapper.Map<TDBEntity>(entity);

        await _context.Set<TDBEntity>().AddAsync(dbModel);
        await _context.SaveChangesAsync();

        return dbModel.Id;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var dbModel = await _context.Set<TDBEntity>().FindAsync(id);

        if (dbModel == null)
            return false;

        _context.Set<TDBEntity>().Remove(dbModel);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<ICollection<TEntity>> GetAllAsync()
    {
        return await _context.Set<TDBEntity>()
            .AsNoTracking()
            .ProjectTo<TEntity>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<TEntity?> GetByIdAsync(Guid id)
    {
        var dbModel = await _context.Set<TDBEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        return _mapper.Map<TEntity>(dbModel);
    }

    public async Task<TEntity> UpdateAsync(TEntity entity)
    {
        var existingDbModel = await _context.Set<TDBEntity>()
            .FindAsync(entity.Id);

        if (existingDbModel == null)
            throw new KeyNotFoundException($"Entity with id {entity.Id} not found.");

        _mapper.Map(entity, existingDbModel);

        _context.Set<TDBEntity>().Update(existingDbModel);
        await _context.SaveChangesAsync();

        return _mapper.Map<TEntity>(existingDbModel);
    }
}