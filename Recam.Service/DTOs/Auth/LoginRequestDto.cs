using System;

namespace Recam.Service.DTOs.Auth;

public class LoginRequestDto
{
    public string UserName { get; set; }
    public string Password { get; set; }
}
