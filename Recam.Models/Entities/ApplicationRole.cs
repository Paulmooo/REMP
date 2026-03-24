using System;

namespace Recam.Models.Entities;

public class ApplicationRole
{
    public string RoleName { get; set; }

    public List<ApplicationUserRole> UserRoles { get; set; }
}
