using System.Text;
using Application.Interfaces.Auth;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Interfaces.Services.HelperServices;
using Application.Interfaces.Services.MainServices;
using Application.Services;
using Application.Services.HelperServices;
using Application.Services.MainServices;
using Application.Services.Options;
using AutoMapper;
using HabitHub.Profiles;
using Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Persistence.DataAccess;
using Persistence.DataAccess.Repositories;
using StackExchange.Redis;

namespace HabitHub.Extensions;

public static class ApiExtensions
{
    public static void AddAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(nameof(JwtOptions)));

        var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions!.SecretKey))
                };
            });

        services.AddAuthorization();

        services.AddSingleton<IJwtWorker, JwtWorker>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
    }

    public static void AddGoogle(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var config = configuration.GetValue<string>("Redis:ConnectionString");
            return ConnectionMultiplexer.Connect(config!);
        });
        
        services.AddSingleton<IGoogleTokenStore, RedisGoogleTokenStore>();
        services.AddSingleton<IGoogleFitService, GoogleFitService>();
        services.AddHttpClient();
    }

    public static void AddGigaChat(this IServiceCollection services)
    {
        services.AddSingleton<IGigaChatApiClient, GigaChatApiClient>();
        services.AddSingleton<IAiService, GigaChatAiService>();
    }

    public static void AddMinio(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MinioOptions>(configuration.GetSection("Minio"));
        services.AddSingleton<IMinioService, MinioService>();
    }

    public static void AddServices(this IServiceCollection services)
    {
        services.AddScoped<IHabitService, HabitService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddSingleton<IGoogleService, GoogleService>();
        services.AddAutoMapper(typeof(UserMappingProfile).Assembly);
    }

    public static void AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options => 
        {
            options.UseNpgsql(configuration.GetConnectionString(nameof(AppDbContext)));
        });

        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<IHabitRepository, HabitRepository>();
        services.AddScoped<ILikeRepository, LikeRepository>();
        services.AddScoped<IMediaFileRepository, MediaFileRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IHabitProgressRepository, HabitProgressRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
    }
}