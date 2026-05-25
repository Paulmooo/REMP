using System;

namespace Remp.Service.DTOs.User;

public class UserInfoDto
{
    public string Id { get; set; }
    public List<string> Roles { get; set; }
    public List<int> ListingCaseIds { get; set; }
}
