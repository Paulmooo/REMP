using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Recam.API;
using Recam.DataAccess.Data;
using Recam.Models.Entities;
using Recam.Repository.Interfaces;
using Recam.Repository.Repositories;
using Recam.Service.Interfaces;
using Recam.Service.Mapper;
using Recam.Service.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddAutoMapper(_ => { }, typeof(MappingProfile).Assembly);


builder.Services.AddSingleton<Recam.API.Middlewares.Exceptions.ExceptionHandlingService>();

builder.Services.AddDbContext<RecamDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RecamDb")));

builder.Services.AddIdentity<User, Role>()
    .AddEntityFrameworkStores<RecamDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthorization(options => {
    options.AddPolicy("AdminPolicy",
    policy => policy.RequireClaim(ClaimTypes.Role, "Admin"));
    options.AddPolicy("UserPolicy",
    policy => policy.RequireClaim(ClaimTypes.Role, "Agent"));
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedRolesAsync(scope.ServiceProvider);
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionHandlingService = context.RequestServices.GetRequiredService<Recam.API.Middlewares.Exceptions.ExceptionHandlingService>();
        await exceptionHandlingService.HandleExceptionAsync(context);
    });
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
