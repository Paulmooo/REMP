using System;
using Remp.Models.Enums;

namespace Remp.Service.DTOs.ListingCase;

public class MediaAssetDto
{
    public int Id { get; set; }
    public MediaType MediaType { get; set; }
    public string MediaUrl { get; set; }
    public DateTime UploadedAt { get; set; }
}
