using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;

namespace HabitHub.Endpoints;

public static class PagesEndpoints
{
    public static void AddPages(this IEndpointRouteBuilder app)
    {
        app.MapGet("/login", async context =>
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync("wwwroot/pages/auth/login.html");
        });
        
        app.MapGet("/register", async context =>
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync("wwwroot/pages/auth/register.html");
        });
        
        app.MapGet("/main", async context =>
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync("wwwroot/pages/main/main.html");
        });
        
        app.MapGet("/habits", async context =>
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync("wwwroot/pages/habit/habit.html");
        });
        
        app.MapGet("/profile/{userId:guid}", async context =>
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync("wwwroot/pages/profile/profile.html");
        });
        
        app.MapGet("/auth/google", async (HttpContext _) =>
        {
            return Results.Challenge(new AuthenticationProperties
            {
                RedirectUri = "/api/google/token/add"
            }, [GoogleDefaults.AuthenticationScheme]);
        });
    }
}