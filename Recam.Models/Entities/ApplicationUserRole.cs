using System;
using Microsoft.AspNetCore.Identity;

namespace Recam.Models.Entities;

public class ApplicationUserRole : IdentityUserRole<string>
{
    //FIXME： 报错IdentityUserRole<string> 已经有同名成员 RoleId
    // public string RoleId { get; set; }
    // public string UserId { get; set; }

    public ApplicationUser User { get; set; }
    public ApplicationRole Role { get; set; }
    
}
