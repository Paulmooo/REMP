using System;
using Remp.Models.Enums;

namespace Remp.Service.DTOs.ListingCase;

public class MediaAssetGroupDto
{
    public MediaType MediaType { get; set; }
    public List<MediaAssetDto> Items { get; set; } = new();
}

