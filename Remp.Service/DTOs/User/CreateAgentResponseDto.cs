namespace Remp.Service.DTOs.User;

public class CreateAgentResponseDto
{
    public string AgentId { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public string TemporaryPassword { get; set; }
}
