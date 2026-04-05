using System;

namespace Recam.Service.DTOs;

public class AuthResponseDto
{
    public string Token { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public List<string> Roles { get; set; } = new List<string>();

    public bool Success { get; set; }
    public string Message { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
}
