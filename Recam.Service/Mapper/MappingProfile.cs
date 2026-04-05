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
        CreateMap<RegisterRequestDto, User>();
    }
}
