using System;

namespace Recam.Service.DTOs;

public class RegisterRequestDto
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
}
