using System;
using AutoMapper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recam.Models.Entities;
using Recam.Models.Enums;
using Recam.Repository.Interfaces;
using Recam.Service.DTOs;
using Recam.Service.Interfaces;

namespace Recam.Service.Services;

public class ListingCaseService : IListingCaseService
{
    private readonly IListingCaseRepository _listingCaseRepository;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateListingCaseRequestDto> _createValidator;
    private readonly IValidator<UpdateListingCaseRequestDto> _updateValidator;

    public ListingCaseService(
        IListingCaseRepository listingCaseRepository,
        IMapper mapper,
        IValidator<CreateListingCaseRequestDto> createValidator,
        IValidator<UpdateListingCaseRequestDto> updateValidator)
    {
        _listingCaseRepository = listingCaseRepository;
        _mapper = mapper;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<CreateListingCaseResponseDto> CreateListingCaseAsync(CreateListingCaseRequestDto dto, string userId)
    {
        var validationResult = await _createValidator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new ArgumentException(errors);
        }

        var userExists = await _listingCaseRepository.UserExistsAsync(userId);
        if (!userExists)
        {
            throw new UnauthorizedAccessException("The user in the JWT token does not exist.");
        }

        var listingCase = _mapper.Map<ListingCase>(dto);
        listingCase.CreatedAt = DateTime.UtcNow;
        listingCase.IsDeleted = false;
        listingCase.UserId = userId;
        listingCase.ListingStatus = ListcaseStatus.Created;

        int newId = await _listingCaseRepository.CreateListingCaseAsync(listingCase);

        return new CreateListingCaseResponseDto
        {
            Id = newId,
            CreatedAt = listingCase.CreatedAt
        };
    }

    public async Task UpdateListingCaseAsync(int id, UpdateListingCaseRequestDto dto, string userId)
    {
        var validationResult = await _updateValidator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new ArgumentException(errors);
        }

        var userExists = await _listingCaseRepository.UserExistsAsync(userId);
        if (!userExists)
        {
            throw new UnauthorizedAccessException("The user in the JWT token does not exist.");
        }

        var existingCase = await _listingCaseRepository.GetListingCaseByIdAsync(id);
        if (existingCase == null)
        {
            throw new KeyNotFoundException($"Listing case with ID {id} not found.");
        }

        if (existingCase.IsDeleted)
        {
            throw new ArgumentException("Deleted listing cases cannot be updated.");
        }

        if (existingCase.ListingStatus == ListcaseStatus.Delivered)
        {
            throw new ArgumentException("Listing case is delivered and cannot be updated.");
        }

        _mapper.Map(dto, existingCase);

        await _listingCaseRepository.UpdateListingCaseAsync(existingCase);
    }
}
