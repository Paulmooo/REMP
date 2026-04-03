using System;
using Microsoft.AspNetCore.Identity;

namespace Recam.Models.Entities;

public class Role : IdentityRole
{
    public string RoleName { get; set; }

    public List<User> Users { get; set; }
}
