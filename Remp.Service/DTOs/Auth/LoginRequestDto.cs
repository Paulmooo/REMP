using System;

namespace Remp.Service.DTOs.Auth;

public class LoginRequestDto
{
    public string UserName { get; set; }
    public string Password { get; set; }
}
