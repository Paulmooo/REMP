using System;

namespace Remp.Service.DTOs.ListingCase;

public class CaseContactDto
{
    public int ContactId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string CompanyName { get; set; }
    public string ProfileUrl { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
}
