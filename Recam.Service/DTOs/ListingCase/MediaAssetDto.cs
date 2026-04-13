using System;
using Recam.Models.Enums;

namespace Recam.Service.DTOs.ListingCase;

public class MediaAssetDto
{
    public int Id { get; set; }
    public MediaType MediaType { get; set; }
    public string MediaUrl { get; set; }
    public DateTime UploadedAt { get; set; }
}
