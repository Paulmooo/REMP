namespace Remp.Service.DTOs.ListingCase;

public class ListingAgentAssignmentDto
{
    public int ListingCaseId { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public bool Assigned { get; set; }
}
