using System;
using System.ComponentModel.DataAnnotations;

namespace Remp.Models.Entities;

public class CaseContact
{
    [Key]
    public int ContactId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string CompanyName { get; set; }
    public string ProfileUrl { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public int ListingCaseId { get; set; }
    
    public ListingCase ListingCase { get; set; }
}
