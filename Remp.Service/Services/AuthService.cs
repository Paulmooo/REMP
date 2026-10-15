using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Remp.Models.Entities;
using Remp.Common.Exceptions;
using Remp.Repository.Interfaces;
using Remp.Service.DTOs.Auth;
using Remp.Service.Interfaces;

namespace Remp.Service.Services;

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IAuthRepository _authRepository;
    private readonly IMapper _mapper;

    public AuthService(IConfiguration configuration, IAuthRepository authRepository, IMapper mapper)
    {
        _configuration = configuration;
        _authRepository = authRepository;
        _mapper = mapper;
    }

    public Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto dto) =>
        throw new ForbiddenException("Public registration is disabled. Agent accounts must be created by a photography company.");

    private string GenerateJwtToken(User user, List<string> roles)
    {
        // 创建JWT声明，包含用户信息和角色
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim("uid", user.Id)
        };

        // 把角色添加到JWT声明中
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // 配置文件中获取key
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        
        // 通过加密算法和key生成签名凭证
        var signature = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // 创建jwt
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: signature
        );

        // 生成jwt字符串
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            throw new ArgumentException("A valid email and password are required.");
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(dto.Email.Trim()))
            throw new ArgumentException("A valid email and password are required.");
        var user = await _authRepository.FindByEmailAsync(dto.Email.Trim());
        if (user == null || user.IsDeleted || (user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow))
        {
            throw new UnauthorizedAccessException("Invalid login credentials.");
        }

        var isValid = await _authRepository.CheckPasswordAsync(user, dto.Password);
        if (!isValid)
        {
            throw new UnauthorizedAccessException("Invalid login credentials.");
        }

        var roles = await _authRepository.GetRolesAsync(user);
        if (roles.Count == 0 || !await _authRepository.HasRequiredProfilesAsync(user, roles))
            throw new ForbiddenException("Account roles and profiles are inconsistent.");
        var token = GenerateJwtToken(user, roles.ToList());
        return new LoginResponseDto
        {
            Token = token,
            UserName = user.UserName,
            Email = user.Email,
            Roles = roles.ToList()
        };
    }

    public async Task<PagedUsersResponseDto> GetAllUsersAsync(int pageNumber, int pageSize)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;
        
        var totalCount = await _authRepository.GetUserCountAsync();
        var currentPageUsers = await _authRepository.GetUsersPagedAsync((pageNumber - 1) * pageSize, pageSize);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = _mapper.Map<List<UserListItemDto>>(currentPageUsers);

        for (int i = 0; i < currentPageUsers.Count; i++)
        {
            items[i].Roles = await _authRepository.GetRolesAsync(currentPageUsers[i]);
        }

        return new PagedUsersResponseDto
        {
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Users = items
        };
    }

}
