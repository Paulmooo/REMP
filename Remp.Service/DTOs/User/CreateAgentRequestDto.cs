using System;
using Microsoft.Extensions.Primitives;

namespace Remp.Service.DTOs.User;

public class CreateAgentRequestDto
{
    public string Email { get; set; }
    public string AgentFirstName { get; set; }
    public string AgentLastName { get; set; }
    public string AvatarUrl { get; set; }
}
