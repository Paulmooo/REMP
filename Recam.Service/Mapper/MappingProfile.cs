using System;
using AutoMapper;
using Recam.Models.Entities;
using Recam.Service.DTOs;
using Recam.Service.DTOs.ListingCase;

namespace Recam.Service.Mapper;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<RegisterRequestDto, User>();
        CreateMap<User, UserListItemDto>()
            .ForMember(dest => dest.Roles, opt => opt.Ignore());

        CreateMap<CreateListingCaseRequestDto, ListingCase>();
        CreateMap<UpdateListingCaseRequestDto, ListingCase>();
    }
}
