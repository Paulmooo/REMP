using System;

namespace Remp.Models.Entities;

public class PhotographyCompany
{
    public string Id { get; set; }
    public string PhotographyCompanyName { get; set; }
    public User User { get; set; }
    public List<Agent> Agents { get; set; }
}
