using System;
using Remp.Models.Entities;
using Remp.Models.Enums;

namespace Remp.Service.DTOs.ListingCase;

public class ListingCaseItemDto
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public int PostCode { get; set; }
    public decimal Longitude { get; set; }
    public decimal Latitude { get; set; }
    public double Price { get; set; }
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public int Garages { get; set; }
    public double FloorArea { get; set; }
    public PropertyType PropertyType { get; set; }
    public SaleCategory SaleCategory { get; set; }
    public ListcaseStatus ListingStatus { get; set; }

    public List<AgentBriefDto> Agents { get; set; }
    public List<CaseContactDto> CaseContacts { get; set; }
    public List<MediaAssetDto> MediaAssets { get; set; } 
}
