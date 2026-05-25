using System.Security.Claims;
using System.Text;
using Azure.Storage.Blobs;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Remp.API;
using Remp.DataAccess.Data;
using Remp.Models.Entities;
using Remp.Repository.Interfaces;
using Remp.Repository.Repositories;
using Remp.Service.Interfaces;
using Remp.Service.Mapper;
using Remp.Service.Services;
using Remp.Service.Validators;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IListingCaseRepository, ListingCaseRepository>();
builder.Services.AddScoped<IListingCaseService, ListingCaseService>();
builder.Services.AddScoped<IMediaAssetRepository, MediaAssetRepository>();
builder.Services.AddScoped<IMediaAssetService, MediaAssetService>();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(x => new BlobServiceClient(
    builder.Configuration.GetSection("AzureBlobStorage")["ConnectionString"])
);
builder.Services.AddAutoMapper(_ => { }, typeof(MappingProfile).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<CreateListingCaseRequestValidator>();

builder.Services.AddSingleton<Remp.API.Middlewares.Exceptions.ExceptionHandlingService>();

builder.Services.AddDbContext<RempDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RempDb")));

builder.Services.AddIdentity<User, Role>()
    .AddEntityFrameworkStores<RempDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthorization(options => {
    options.AddPolicy("AdminPolicy",
    policy => policy.RequireClaim(ClaimTypes.Role, "Admin"));
    options.AddPolicy("UserPolicy",
    policy => policy.RequireClaim(ClaimTypes.Role, "User"));
    options.AddPolicy("AgentPolicy",
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
        var exceptionHandlingService = context.RequestServices.GetRequiredService<Remp.API.Middlewares.Exceptions.ExceptionHandlingService>();
        await exceptionHandlingService.HandleExceptionAsync(context);
    });
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
