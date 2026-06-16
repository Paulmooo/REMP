using System;

namespace Remp.Service.DTOs.Auth;

public class UserListItemDto
{
    public string Id { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public List<string> Roles { get; set; }
}
