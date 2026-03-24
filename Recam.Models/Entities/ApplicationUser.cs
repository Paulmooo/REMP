using System;
using Microsoft.AspNetCore.Identity;

namespace Recam.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }

    public Agent Agent { get; set; }
    public List<ListingCase> ListingCases { get; set; }
    public ApplicationUser User { get; set; }
    public List<MediaAsset> MediaAssets { get; set; }

}
