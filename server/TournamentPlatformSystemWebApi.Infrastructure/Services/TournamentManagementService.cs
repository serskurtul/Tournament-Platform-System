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

public class TournamentManagementService : ITournamentManagementService
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IThemeRepository _themeRepository;
    private readonly IStorageService _storageService;
    private readonly IParticipantRoster _participantRoster;

    public TournamentManagementService(ITournamentRepository tournamentRepository, IThemeRepository themeRepository, IStorageService storageService, IParticipantRoster participantRoster)
    {
        _tournamentRepository = tournamentRepository;
        _themeRepository = themeRepository;
        _storageService = storageService;
        _participantRoster = participantRoster;
    }

    public async Task<TournamentDto> CreateTournamentAsync(TournamentCreateDto dto, Guid organizerId)
    {
        if (dto == null)
            throw new ValidationException("Tournament data is required");

        var title = dto.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new ValidationException("Title is required");

        // Ensure unique title per organizer
        var unique = await _tournamentRepository.IsTitleUniqueAsync(title, organizerId);
        if (!unique)
            throw new DuplicateTournamentTitleException("Tournament title must be unique for the organizer");

        // DEV-342 [bug] Do not allow creating a tournament with a start date in the past
        var startUtc = dto.StartDate.ToUniversalTime().Date;
        if (startUtc < DateTime.UtcNow.Date)
            throw new ValidationException("Start date cannot be in the past");

        var entity = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = title,
            Description = dto.Description,
            Conditions = dto.Conditions,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            RegistrationDeadline = dto.RegistrationCloseDate,
            MaxTeams = dto.MaxParticipants,
            Status = TournamentStatus.REGISTRATION_OPEN,
            OrganizerId = organizerId,
            ThemeId = dto.Sport
        };

        if (!dto.Sport.HasValue || !await _themeRepository.IsSportWithId(dto.Sport.Value))
        {
            throw new ArgumentException("Sport with this Id not found");
        }

        var createdId = await _tournamentRepository.CreateAsync(entity);

        var newTournamentWithDetails = await _tournamentRepository.GetByIdAsync(createdId);
        if (newTournamentWithDetails == null)
            throw new InvalidOperationException("Created tournament could not be loaded");

        var result = new TournamentDto
        {
            Id = createdId,
            Title = entity.Name,
            Description = entity.Description,
            Conditions = entity.Conditions,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            RegistrationCloseDate = entity.RegistrationDeadline,
            SportId = newTournamentWithDetails.ThemeId,
            SportName = newTournamentWithDetails.ThemeName,
            MaxParticipants = entity.MaxTeams,
            Status = "registration_open",
            OrganizerId = organizerId,
            OrganizerName = newTournamentWithDetails.OrganizerName ?? string.Empty
        };

        return result;
    }

    public async Task<TournamentDto> SaveTournamentDraftAsync(TournamentCreateDto dto, Guid organizerId)
    {
        if (dto == null)
            throw new ValidationException("Tournament data is required");

        var title = dto.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new ValidationException("Title is required");


        var entity = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = title,
            Description = dto.Description,
            Conditions = dto.Conditions,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            RegistrationDeadline = dto.RegistrationCloseDate,
            MaxTeams = dto.MaxParticipants,
            Status = TournamentStatus.DRAFT,
            OrganizerId = organizerId,
            ThemeId = dto.Sport
        };

        var createdId = await _tournamentRepository.CreateAsync(entity);

        var newTournamentWithDetails = await _tournamentRepository.GetByIdAsync(createdId);
        if (newTournamentWithDetails == null)
            throw new InvalidOperationException("Created tournament draft could not be loaded");

        var result = new TournamentDto
        {
            Id = createdId,
            Title = entity.Name,
            Description = entity.Description,
            Conditions = entity.Conditions,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            RegistrationCloseDate = entity.RegistrationDeadline,
            SportId = newTournamentWithDetails.ThemeId,
            SportName = newTournamentWithDetails.ThemeName,
            MaxParticipants = entity.MaxTeams,
            Status = "draft",
            OrganizerId = organizerId,
            OrganizerName = newTournamentWithDetails.OrganizerName ?? string.Empty
        };

        return result;
    }

    public async Task<StorageUploadResult> UploadImageAsync(Guid tournamentId, Guid organizerId, System.IO.Stream stream, string fileName, string contentType, long length)
    {
        // validate size and content type
        var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (length <= 0 || length > 5 * 1024 * 1024) throw new ValidationException("File is empty or too large (max 5MB)");
        if (Array.IndexOf(allowed, contentType) < 0) throw new ValidationException("Unsupported file type");

        var existing = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (existing == null) throw new KeyNotFoundException("Tournament not found");
        if (existing.OrganizerId != organizerId) throw new UnauthorizedAccessException("Not the organizer");

        var ext = System.IO.Path.GetExtension(fileName) ?? string.Empty;
        var safeName = $"tournament_{tournamentId}_{Guid.NewGuid()}{ext}";

        var upload = await _storageService.UploadAsync(stream, safeName, contentType);

        existing.BackgroundImg = upload.Url;
        await _tournamentRepository.UpdateAsync(existing);

        return upload;
    }

    public async Task<TournamentPlatformSystemWebApi.Application.DTOs.TournamentDto> UpdateTournamentAsync(Guid tournamentId, TournamentCreateDto dto, Guid organizerId)
    {
        if (dto == null)
            throw new ValidationException("Tournament data is required");

        var existing = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (existing == null) throw new KeyNotFoundException("Tournament not found");
        if (existing.OrganizerId != organizerId) throw new UnauthorizedAccessException("Not the organizer");

        // Do not allow editing when tournament already in progress or completed
        if (existing.Status == TournamentStatus.IN_PROGRESS || existing.Status == TournamentStatus.COMPLETED)
            throw new TournamentPlatformSystemWebApi.Common.Exceptions.TournamentAlreadyStartedException("Tournament cannot be edited in its current status");

        var title = dto.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new ValidationException("Title is required");

        // If title changed, ensure uniqueness for organizer
        if (!string.Equals(existing.Name?.Trim(), title, StringComparison.InvariantCultureIgnoreCase))
        {
            var unique = await _tournamentRepository.IsTitleUniqueAsync(title, organizerId);
            if (!unique)
                throw new TournamentPlatformSystemWebApi.Common.Exceptions.DuplicateTournamentTitleException("Tournament title must be unique for the organizer");
        }

        // Validate sport exists
        if (!dto.Sport.HasValue || !await _themeRepository.IsSportWithId(dto.Sport.Value))
        {
            throw new ArgumentException("Sport with this Id not found");
        }

        // Check participants count vs new limit
        var participantsCount = await _participantRoster.CountActiveAsync(tournamentId);
        if (dto.MaxParticipants < participantsCount)
            throw new TournamentPlatformSystemWebApi.Common.Exceptions.InsufficientParticipantsException("New max participants is less than current participants count");

        // map updated fields
        existing.Name = title;
        existing.Description = dto.Description;
        existing.Conditions = dto.Conditions;
        existing.StartDate = dto.StartDate;
        existing.EndDate = dto.EndDate;
        existing.RegistrationDeadline = dto.RegistrationCloseDate;
        existing.MaxTeams = dto.MaxParticipants;
        existing.ThemeId = dto.Sport;

        var updated = await _tournamentRepository.UpdateAsync(existing);

        var newTournamentWithDetails = await _tournamentRepository.GetByIdAsync(updated.Id);
        if (newTournamentWithDetails == null)
            throw new InvalidOperationException("Updated tournament could not be loaded");

        var result = new TournamentPlatformSystemWebApi.Application.DTOs.TournamentDto
        {
            Id = updated.Id,
            Title = updated.Name,
            Description = updated.Description,
            Conditions = updated.Conditions,
            StartDate = updated.StartDate,
            EndDate = updated.EndDate,
            RegistrationCloseDate = updated.RegistrationDeadline,
            SportId = newTournamentWithDetails.ThemeId,
            SportName = newTournamentWithDetails.ThemeName,
            MaxParticipants = updated.MaxTeams,
            Status = updated.Status.ToString().ToLowerInvariant(),
            OrganizerId = organizerId,
            OrganizerName = newTournamentWithDetails.OrganizerName ?? string.Empty
        };

        return result;
    }
}