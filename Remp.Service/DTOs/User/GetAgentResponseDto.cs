using System;

namespace Remp.Service.DTOs.User;

public class GetAgentResponseDto
{
    public string Id { get; set; } = null!;
    public string AgentFirstName { get; set; } = null!;
    public string AgentLastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string AvatarUrl { get; set; } = null!;
}
