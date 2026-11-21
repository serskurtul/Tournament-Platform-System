using System;
using TournamentPlatformSystemWebApi.Application.DTOs;
using TournamentPlatformSystemWebApi.Core.Entities;

namespace TournamentPlatformSystemWebApi.Application.Interfaces;

/// <summary>Creating, drafting, editing tournaments and their banner image.</summary>
public interface ITournamentManagementService
{
    Task<TournamentDto> CreateTournamentAsync(TournamentCreateDto dto, Guid organizerId);
    Task<TournamentDto> SaveTournamentDraftAsync(TournamentCreateDto dto, Guid organizerId);
    Task<TournamentPlatformSystemWebApi.Application.DTOs.TournamentDto> UpdateTournamentAsync(Guid tournamentId, TournamentCreateDto dto, Guid organizerId);
    Task<StorageUploadResult> UploadImageAsync(Guid tournamentId, Guid organizerId, System.IO.Stream stream, string fileName, string contentType, long length);
}