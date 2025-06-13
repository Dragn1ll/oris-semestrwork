using System.Globalization;
using System.Security.Claims;
using Application.Interfaces.Services.MainServices;
using HabitHub.Requests.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;

namespace HabitHub.Endpoints;

public static class GoogleEndpoints
{
    public static void MapGoogleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/google")
            .RequireAuthorization()
            .WithOpenApi();

        app.MapGet("/signin-google", SignInWithGoogle);
        app.MapGet("/signin-google-callback", GoogleCallback);
        
        group.MapDelete("/token/remove", RemoveGoogleToken);
        group.MapGet("/token/contains", HasTokenAsync);
        group.MapPost("/fit/progress/analyze", GetUserFitProgressAsync);
    }
    
    private static IResult SignInWithGoogle(HttpContext context)
    {
        Console.WriteLine("SignInWithGoogle");
        var redirectUri = "/api/google/signin-google-callback";
        var properties = new AuthenticationProperties { RedirectUri = redirectUri };
        return Results.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
    }
    
    private static async Task<IResult> GoogleCallback(
        HttpContext context,
        IGoogleService googleService
    )
    {
        Console.WriteLine("Google Callback");
        var result = await context.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

        if (!result.Succeeded || result.Principal == null)
            return Results.Problem("Google authentication failed", statusCode: 500);

        var tokens = result.Properties?.GetTokens();
        var accessToken = tokens?.FirstOrDefault(t => t.Name == "access_token")?.Value;
        var refreshToken = tokens?.FirstOrDefault(t => t.Name == "refresh_token")?.Value;
        var expiresAt = tokens?.FirstOrDefault(t => t.Name == "expires_at")?.Value;

        if (string.IsNullOrEmpty(accessToken))
            return Results.Problem("Access token not found", statusCode: 500);

        var userId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var userGuid))
            return Results.Problem("Invalid user ID", statusCode: 500);

        var addResult = await googleService.AddGoogleToken(userGuid, accessToken, refreshToken,
            DateTime.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                out var expiry)
                ? expiry
                : DateTime.UtcNow.AddHours(1));

        return addResult.IsSuccess
            ? Results.Ok("Google tokens saved successfully")
            : Results.Problem(addResult.Error!.Message, statusCode: (int)addResult.Error.ErrorType);
    }

    private static async Task<IResult> RemoveGoogleToken(
        ClaimsPrincipal user,
        IGoogleService googleService
    )
    {
        var userId = GetUserIdFromClaims(user);
        
        var removeResult = await googleService.RemoveGoogleToken(userId!.Value);
        
        return removeResult.IsSuccess
            ? Results.Ok()
            : Results.Problem(removeResult.Error!.Message, statusCode: (int)removeResult.Error.ErrorType);
    }

    private static async Task<IResult> HasTokenAsync(
        ClaimsPrincipal user,
        IGoogleService googleService
    )
    {
        var userId = GetUserIdFromClaims(user);
        
        var hasResult = await googleService.HasTokenAsync(userId!.Value);
        
        return hasResult.IsSuccess
            ? Results.Ok(hasResult.Value)
            : Results.Problem(hasResult.Error!.Message, statusCode: (int)hasResult.Error.ErrorType);
    }

    private static async Task<IResult> GetUserFitProgressAsync(
        ClaimsPrincipal user,
        [FromBody] FitAnalyzeRequest request,
        IGoogleService googleService
    )
    {
        var userId = GetUserIdFromClaims(user);
        
        var getResult = await googleService.GetUserFitProgressAsync(userId!.Value, request.Goal);
        
        return getResult.IsSuccess
            ? Results.Ok(getResult.Value)
            : Results.Problem(getResult.Error!.Message, statusCode: (int)getResult.Error.ErrorType);
    }
    
    private static Guid? GetUserIdFromClaims(ClaimsPrincipal user)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idStr, out var guid) ? guid : null;
    }
}