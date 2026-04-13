using System;

namespace Recam.Service.DTOs.ListingCase;

public class AgentBriefDto
{
    public string Id { get; set; }
    public string AgentFirstName { get; set; }
    public string AgentLastName { get; set; }
    public string AvatarUrl { get; set; }
    public string CompanyName { get; set; }
}
