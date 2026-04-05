using System;
using AutoMapper;
using Azure;
using Recam.Models.Entities;
using Recam.Service.DTOs;

namespace Recam.Service.Mapper;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<RegisterRequestDto, User>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Username));

    }
}
