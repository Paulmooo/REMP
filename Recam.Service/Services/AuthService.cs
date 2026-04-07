using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Recam.Models.Entities;
using Recam.Repository.Interfaces;
using Recam.Service.DTOs;
using Recam.Service.Interfaces;

namespace Recam.Service.Services;

public class AuthService : IAuthService
{
    private const string DefaultUserRole = "Agent";
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
        var existing = await _authRepository.FindByUsernameAsync(dto.Username);
        if (existing != null)
        {
            throw new ArgumentException("Username already exists");
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
            Username = newUser.UserName,
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
            new Claim(ClaimTypes.NameIdentifier, user.Id)
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

}
