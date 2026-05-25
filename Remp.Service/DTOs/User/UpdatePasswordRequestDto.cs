namespace Remp.Service.DTOs.User;

public class UpdatePasswordRequestDto
{
    public string CurrentPassword { get; set; }
    public string NewPassword { get; set; }
}
