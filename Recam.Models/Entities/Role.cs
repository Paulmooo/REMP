using System;

namespace Recam.Models.Entities;

public class Role
{
    public string RoleName { get; set; }

    public List<User> Users { get; set; }
}
