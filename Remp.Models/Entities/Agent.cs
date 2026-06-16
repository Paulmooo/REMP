using System;

namespace Remp.Models.Entities;

public class Agent
{
    public string Id { get; set; }
    public string AgentFirstName { get; set; }
    public string AgentLastName { get; set; }
    public string AvatarUrl { get; set; }
    public string CompanyName { get; set; }
    public User User { get; set; }
    public List<ListingCase> ListingCases { get; set; }
    public List<PhotographyCompany> PhotographyCompanies { get; set; }
}
