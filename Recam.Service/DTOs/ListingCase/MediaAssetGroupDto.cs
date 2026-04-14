using System;
using Recam.Models.Enums;

namespace Recam.Service.DTOs.ListingCase;

public class MediaAssetGroupDto
{
    public MediaType MediaType { get; set; }
    public List<MediaAssetDto> Items { get; set; } = new();
}

