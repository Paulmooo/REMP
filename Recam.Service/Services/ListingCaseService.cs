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
    private readonly IValidator<CreateListingCaseRequestDto> _validator;

    public ListingCaseService(
        IListingCaseRepository listingCaseRepository,
        IMapper mapper,
        IValidator<CreateListingCaseRequestDto> validator)
    {
        _listingCaseRepository = listingCaseRepository;
        _mapper = mapper;
        _validator = validator;
    }

    public async Task<CreateListingCaseResponseDto> CreateListingCaseAsync(CreateListingCaseRequestDto dto, string userId)
    {
        var validationResult = await _validator.ValidateAsync(dto);
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

        int newId;
        try
        {
            newId = await _listingCaseRepository.CreateListingCaseAsync(listingCase);
        }
        catch (DbUpdateException ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            throw new ArgumentException($"Database write failed: {detail}");
        }

        return new CreateListingCaseResponseDto
        {
            Id = newId,
            CreatedAt = listingCase.CreatedAt
        };
    }

}
