using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Remp.Models.Entities;
using Remp.Repository.Interfaces;
using Remp.Service.DTOs.Auth;
using Remp.Service.Interfaces;

namespace Remp.Service.Services;

public class AuthService : IAuthService
{
    private const string DefaultUserRole = "User";
    private readonly IConfiguration _configuration;
    private readonly IAuthRepository _authRepository;
    private readonly IMapper _mapper;

    public AuthService(IConfiguration configuration, IAuthRepository authRepository, IMapper mapper)
    {
        _configuration = configuration;
        _authRepository = authRepository;
        _mapper = mapper;
    }

    public async Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto dto)
    {
        var existing = await _authRepository.FindByUsernameAsync(dto.UserName);
        if (existing != null)
        {
            throw new ArgumentException("UserName already exists");
        }

        var newUser = _mapper.Map<User>(dto);
        
        var result = await _authRepository.RegisterWithRoleAsync(newUser, dto.Password, DefaultUserRole);
        if (!result.Succeeded)
        {
            throw new ArgumentException("Failed to register user");
        }

        var roles = await _authRepository.GetRolesAsync(newUser);
        var token = GenerateJwtToken(newUser, roles.ToList());

        return new RegisterResponseDto
        {
            Token = token,
            UserName = newUser.UserName,
            Email = newUser.Email,
            Roles = roles.ToList()
        };
    }

    private string GenerateJwtToken(User user, List<string> roles)
    {
        // 创建JWT声明，包含用户信息和角色
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
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
        var user = await _authRepository.FindByUsernameAsync(dto.UserName);
        if (user == null)
        {
            throw new ArgumentException("User not found");
        }

        var isValid = await _authRepository.CheckPasswordAsync(user, dto.Password);
        if (!isValid)
        {
            throw new ArgumentException("Invalid password");
        }

        var roles = await _authRepository.GetRolesAsync(user);
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
