using System;

namespace Recam.Models.Entities;

public class PhotograpyCompany
{
    public string Id { get; set; }
    public string PhotographyCompanyName { get; set; }

    // FIXME: is是什么意思
    // public ApplicationUser User { get; set; }
    public List<Agent> Agents { get; set; }
}
