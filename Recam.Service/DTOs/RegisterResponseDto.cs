using System;

namespace Recam.Service.DTOs;

public class RegisterResponseDto
{
    public string Token { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public List<string> Roles { get; set; } = new List<string>();
}
