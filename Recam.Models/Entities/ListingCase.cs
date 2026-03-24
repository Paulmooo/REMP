using System;
using Recam.Models.Enums;

namespace Recam.Models.Entities;

public class ListingCase
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
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public PropertyType PropertyType { get; set; }
    public SaleCategory SaleCategory { get; set; }
    public ListcaseStatus ListingStatus { get; set; }
    public string UserId { get; set; } //FIXME: 怎么和agent对应上

    public ApplicationUser User { get; set; }
    public List<Agent> Agents { get; set; }
    public List<CaseContact> CaseContacts { get; set; }
    public List<MediaAsset> MediaAssets { get; set; } 

}
