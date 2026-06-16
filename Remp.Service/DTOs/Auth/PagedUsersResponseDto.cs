using System;

namespace Remp.Service.DTOs.Auth;

public class PagedUsersResponseDto
{
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public List<UserListItemDto> Users { get; set; }
}
