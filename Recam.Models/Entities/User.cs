using System;
using Microsoft.AspNetCore.Identity;

namespace Recam.Models.Entities;

public class User : IdentityUser
{
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }

    public Agent Agent { get; set; }
    public List<ListingCase> ListingCases { get; set; }
    public List<Role> Roles { get; set; }
    public List<MediaAsset> MediaAssets { get; set; }

}
