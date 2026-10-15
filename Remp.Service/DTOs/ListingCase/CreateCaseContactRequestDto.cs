using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Remp.Service.DTOs.ListingCase;

// Reject identity/ownership fields rather than silently accepting an over-posted body.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateCaseContactRequestDto
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;
    [StringLength(200)]
    public string? CompanyName { get; set; }
    [StringLength(1000)]
    public string? ProfileUrl { get; set; }
    [StringLength(256)]
    public string? Email { get; set; }
    [StringLength(32)]
    public string? PhoneNumber { get; set; }
}
