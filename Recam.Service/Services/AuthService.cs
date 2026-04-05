using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Recam.Models.Entities;
using Recam.Repository.Interfaces;
using Recam.Service.DTOs;
using Recam.Service.Interfaces;

namespace Recam.Service.Services;

public class AuthService : IAuthService
{
    private const string DefaultUserRole = "User";
    private readonly IConfiguration _configuration;
    private readonly IAuthRepository _authRepository;
    

    public AuthService(IConfiguration configuration, IAuthRepository authRepository)
    {
        _configuration = configuration;
        _authRepository = authRepository;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto)
    {
        var existing = await _authRepository.FindByUsernameAsync(dto.Username);
        if (existing != null)
        {
            return new AuthResponseDto { Success = false, Message = "Username already exists" };
        }

        var newUser = new User
        {
            UserName = dto.Username,
            Email = dto.Email
        };

        var result = await _authRepository.RegisterWithRoleAsync(newUser, dto.Password, DefaultUserRole);
        if (!result.Succeeded)
        {
            return new AuthResponseDto
            {
                Success = false,
                Errors = result.Errors.Select(e => e.Description).ToList()
            };
        }

        var token = await GenerateJwtToken(newUser);

        return new AuthResponseDto
        {
            Success = true,
            Token = token,
            Username = newUser.UserName,
            Email = newUser.Email
        };
    }

    private async Task<string> GenerateJwtToken(User user)
    {
        // 创建JWT声明，包含用户信息和角色
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id)
        };

        // 获取用户的角色并添加到声明中
        var roles = await _authRepository.GetRolesAsync(user);

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
