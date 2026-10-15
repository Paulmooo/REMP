using System.ComponentModel.DataAnnotations;

namespace Remp.Service.DTOs.ListingCase;

public class SaveMediaSelectionRequestDto
{
    [Required]
    [MaxLength(10)]
    public List<int> MediaIds { get; set; } = null!;
    public int? HeroMediaId { get; set; }
}

public class SetHeroRequestDto
{
    [Required]
    public int? MediaId { get; set; }
}
