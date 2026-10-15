namespace Remp.Service.DTOs.ListingCase;

public class FinalSelectionDto
{
    public int ListingCaseId { get; set; }
    public List<MediaAssetDto> Pictures { get; set; } = [];
    public MediaAssetDto? Hero { get; set; }
}
