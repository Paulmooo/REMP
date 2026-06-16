using System;
using AutoMapper;
using Remp.Models.Entities;
using Remp.Service.DTOs.Auth;
using Remp.Service.DTOs.ListingCase;
using Remp.Service.DTOs.User;

namespace Remp.Service.Mapper;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<RegisterRequestDto, User>();
        CreateMap<User, UserListItemDto>()
            .ForMember(dest => dest.Roles, opt => opt.Ignore());

        CreateMap<CreateListingCaseRequestDto, ListingCase>();
        CreateMap<UpdateListingCaseRequestDto, ListingCase>()
            .ForMember(dest => dest.ListingStatus, opt => opt.Ignore());

        CreateMap<ListingCase, ListingCaseItemDto>();
        CreateMap<MediaAsset, MediaAssetDto>();
        CreateMap<Agent, GetAgentResponseDto>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email));

    }
}
